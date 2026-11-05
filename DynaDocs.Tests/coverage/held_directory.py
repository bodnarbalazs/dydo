"""A directory this process creates and holds open, written only through that handle.

Another process may replace any path at any moment. Each directory here is created and opened
relative to its held parent, each file is created exclusively without following a link, and the
path must still name the held directory when writing ends.
"""
import ctypes as c
from ctypes import wintypes as w
from contextlib import contextmanager
import os
import shutil

SYNCHRONIZE, FILE_LIST_DIRECTORY, FILE_TRAVERSE, FILE_READ_ATTRIBUTES = 0x100000, 0x1, 0x20, 0x80
FILE_GENERIC_WRITE = 0x120116
FILE_SHARE_READ, FILE_SHARE_WRITE, FILE_SHARE_DELETE = 0x1, 0x2, 0x4
OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OPEN_REPARSE_POINT = 3, 0x2000000, 0x200000
FILE_CREATE, OBJ_CASE_INSENSITIVE = 2, 0x40
FILE_DIRECTORY_FILE, FILE_SYNCHRONOUS_IO_NONALERT, FILE_NON_DIRECTORY_FILE = 0x1, 0x20, 0x40
FILE_OPEN_REPARSE_POINT = 0x200000


class UnicodeString(c.Structure):
    _fields_ = [("length", w.USHORT), ("maximum_length", w.USHORT), ("buffer", w.LPWSTR)]


class ObjectAttributes(c.Structure):
    _fields_ = [("length", w.ULONG), ("root", w.HANDLE), ("name", c.POINTER(UnicodeString)),
                ("attributes", w.ULONG), ("security", c.c_void_p), ("quality", c.c_void_p)]


class IoStatus(c.Structure):
    _fields_ = [("status", c.c_void_p), ("information", c.c_size_t)]


def _posix_parent(path):
    return os.open(path, os.O_RDONLY | os.O_DIRECTORY)


def _posix_directory(parent, path):
    unheld = f"Could not hold the directory just created at {path}"
    os.mkdir(path.name, dir_fd=parent)
    try:
        held = os.open(path.name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent)
    except OSError as error:
        raise OSError(f"{unheld}: {error.strerror}") from error
    # POSIX cannot create and open a directory in one call. A swap between the two leaves a link,
    # refused above, or another directory: only an empty one this user owns is indistinguishable
    # from what mkdir made, and it holds nothing to overwrite.
    if os.fstat(held).st_uid == os.geteuid() and not os.listdir(held):
        return held
    os.close(held)
    raise OSError(f"{unheld}: another directory replaced it")


def _posix_file(parent, path):
    return os.open(path.name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o666,
                   dir_fd=parent)


def _windows_open(path, access, flags):
    import msvcrt
    from windows_job import bindings, native_handle
    handle = native_handle(bindings().CreateFileW(
        str(path), access, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, None,
        OPEN_EXISTING, flags, None))
    return msvcrt.open_osfhandle(handle, 0)


def _windows_parent(path):
    return _windows_open(path, FILE_TRAVERSE | SYNCHRONIZE, FILE_FLAG_BACKUP_SEMANTICS)


def _windows_identity(path):
    opened = _windows_open(path, FILE_READ_ATTRIBUTES,
                           FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
    try:
        return os.fstat(opened)
    finally:
        os.close(opened)


def _nt_create(parent, path, access, share, options):
    """Create and open `path`'s name in one call relative to the held directory `parent`, as a C
    runtime descriptor. FILE_CREATE refuses any existing name, a reparse point included."""
    import msvcrt
    ntdll = c.WinDLL("ntdll")
    ntdll.NtCreateFile.restype = c.c_long
    ntdll.NtCreateFile.argtypes = [c.POINTER(w.HANDLE), w.DWORD, c.POINTER(ObjectAttributes),
                                   c.POINTER(IoStatus), c.c_void_p, w.ULONG, w.ULONG, w.ULONG,
                                   w.ULONG, c.c_void_p, w.ULONG]
    ntdll.RtlNtStatusToDosError.argtypes, ntdll.RtlNtStatusToDosError.restype = [c.c_long], w.ULONG
    size = len(path.name.encode("utf-16-le"))
    name = UnicodeString(size, size, path.name)
    attributes = ObjectAttributes(c.sizeof(ObjectAttributes), msvcrt.get_osfhandle(parent),
                                  c.pointer(name), OBJ_CASE_INSENSITIVE, None, None)
    handle, io = w.HANDLE(), IoStatus()
    status = ntdll.NtCreateFile(c.byref(handle), access | SYNCHRONIZE, c.byref(attributes),
                                c.byref(io), None, 0, share, FILE_CREATE,
                                options | FILE_SYNCHRONOUS_IO_NONALERT | FILE_OPEN_REPARSE_POINT,
                                None, 0)
    if status < 0:
        raise c.WinError(ntdll.RtlNtStatusToDosError(status))
    return msvcrt.open_osfhandle(handle.value, 0)


def _windows_directory(parent, path):
    # Without FILE_SHARE_DELETE nothing can rename or delete the directory while it is held; a
    # share mode binds only an open with data access, which listing is.
    return _nt_create(parent, path, FILE_LIST_DIRECTORY | FILE_TRAVERSE | FILE_READ_ATTRIBUTES,
                      FILE_SHARE_READ | FILE_SHARE_WRITE, FILE_DIRECTORY_FILE)


def _windows_file(parent, path):
    return _nt_create(parent, path, FILE_GENERIC_WRITE | FILE_READ_ATTRIBUTES, FILE_SHARE_READ,
                      FILE_NON_DIRECTORY_FILE)


if os.name == "nt":
    _open_parent, _create_directory, _create_file, _identity = (
        _windows_parent, _windows_directory, _windows_file, _windows_identity)
else:
    _open_parent, _create_directory, _create_file, _identity = (
        _posix_parent, _posix_directory, _posix_file, os.lstat)


def _copy_tree(source, parent, path):
    held = _create_directory(parent, path)
    try:
        for entry in sorted(source.iterdir()):
            if entry.is_dir():
                _copy_tree(entry, held, path / entry.name)
                continue
            with entry.open("rb") as origin, open(_create_file(held, path / entry.name), "wb") as target:
                shutil.copyfileobj(origin, target)
    finally:
        os.close(held)


def _names(path, held):
    try:
        return os.path.samestat(_identity(path), os.fstat(held))
    except (FileNotFoundError, NotADirectoryError):
        return False


class Directory:
    """A directory this process created and holds open. Its writes go through the held handle and
    never resolve its path again."""

    def __init__(self, held, path):
        self.held, self.path = held, path

    def copy_tree(self, source, name):
        """Copy the tree at `source` into the new subdirectory `name`, following the links inside
        `source` as shutil.copytree does."""
        _copy_tree(source, self.held, self.path / name)

    def write_text(self, name, text):
        """Write `text` to the new file `name` as Path.write_text would: UTF-8, newlines native."""
        with open(_create_file(self.held, self.path / name), "w", encoding="utf-8") as file:
            file.write(text)


@contextmanager
def create(path):
    """Create the directory at the absolute `path`, its parents as needed, and yield it held as a
    Directory. Raises, naming `path`, when it no longer names that directory once the block ends."""
    path.parent.mkdir(parents=True, exist_ok=True)
    parent = _open_parent(path.parent)
    try:
        held = _create_directory(parent, path)
    finally:
        os.close(parent)
    try:
        yield Directory(held, path)
        if not _names(path, held):
            raise OSError(f"The directory written was replaced at its path: {path}")
    finally:
        os.close(held)
