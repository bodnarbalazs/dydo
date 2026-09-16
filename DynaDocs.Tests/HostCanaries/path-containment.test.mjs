import assert from "node:assert/strict";
import test from "node:test";
import { isInside } from "./path-containment.mjs";

test("disjoint paths where the child string merely starts with a separator after the parent length are not inside", () => {
  assert.equal(
    isInside(
      "C:/Users/User/AppData/Local/DynaDocs/host-canaries",
      "C:/Users/User/Desktop/Projects/DynaDocs/.worktrees/dyd103",
    ),
    false,
  );
});

test("a genuine parent/child containment is inside", () => {
  assert.equal(isInside("C:/a/build", "C:/a/build/sub/file.txt"), true);
});

test("identical paths are inside (same path)", () => {
  assert.equal(isInside("C:/a/build", "C:/a/build"), true);
});

test("a sibling whose name merely shares the parent's name as a prefix is not inside", () => {
  assert.equal(isInside("C:/a/build", "C:/a/buildextra/x"), false);
});

test("containment is case-insensitive on Windows, matching samePath", () => {
  assert.equal(isInside("C:/A/B", "C:/a/b/c"), true);
});

test("a drive-root parent contains its children", () => {
  assert.equal(isInside("C:/", "C:/foo"), true);
});

test("a UNC share-root parent contains its children", () => {
  assert.equal(isInside("//server/share", "//server/share/dir"), true);
});
