# Statechart TLC transport

Original MIT statechart code; [LICENSE](LICENSE) is the repository license.
The upstream statechart notice remains in [the skill license](../../LICENSE).
This helper uses public Java APIs; it contains no copied OpenJDK implementation.

`LoopbackTlc` installs `LoopbackSockets` before passing the original argument array
to unchanged TLC1.7.4. Server sockets bind explicitly to IPv4 `127.0.0.1`.
Client sockets accept only that numeric address or `localhost`, and connect to
the numeric address. Installation, an existing factory, bind and connection
errors propagate; there is no wildcard or raw-TLC fallback. This is local
transport for supported checking, not a sandbox for injected agents or model
extensions. A global factory cannot override a per-object factory.

The design evidence is the public
[RMISocketFactory API](https://docs.oracle.com/en/java/javase/21/docs/api/java.rmi/java/rmi/server/RMISocketFactory.html).
The [source checkpoint](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/checkpoint.md)
records the pinned TLC audit, finite fixtures, current limits and execution hold.

`build-lock.json` is deliberately **unbuilt**. No helper jar is distributed from
this draft. `build.py` accepts only absolute, disjoint owned paths on Linux and
verifies source, TLC and the entire extracted JDK against the locked archive
before invoking its compiler. It neither downloads nor installs anything.
The exact release11 compiler and deterministic two-class packaging recipe is in
the build lock. Two fresh builds must match before an artifact receipt is sealed.
JDK binaries are build-only and are not included in the helper.

The [B1b receipt](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031b/result.md)
retains twelve passing helper tests and the first failed compiler invocation.
The original TLC jar's manifest names `CommunityModules.jar`; that absent
relative classpath entry produced a warning rejected by `-Werror`.
The successor compiler recipe derives `work/compiler-tlc` exclusively from
the unchanged, hash-verified official jar. All1,066 member names, lengths and
raw hashes are independently pinned, including the original manifest bytes.
The new directory's complete membership and bytes are checked before each
compiler invocation. The only compiler classpath element is this directory;
its manifest remains data and contributes no sibling jar search. The release,
warning, processor and deterministic two-class packaging flags stay unchanged.

The [compiler-view source inventory](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/compiler-view-source/tlc-members.json)
records977 class members and no Java source members. Offline class bytes expose
`public static main(String[]) throws java.lang.Exception`; no placeholder or
companion is supplied. Actual compilation still needs the separately inspected
source release. This source correction and its changed tests have not executed.
The prior passing12-test receipt is not a verdict for these newer helper bytes.
The retained pinned JDK archive must be reverified and reused at that boundary,
with a new owned read-only mounted toolchain; no replacement download is needed.

This exploded view is compiler-only. Runtime still consumes the byte-identical
official jar. Its manifest dependency requires the separately governed checker
data isolation and precise external-companion diagnostic; compiler representation
does not silently change accepted model-library semantics or close that boundary.

The separately authorized [runtime source checkpoint](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/runtime-isolation/checkpoint.md)
records that boundary's draft implementation. Runtime copies only the original
jar DATA into a new plain registered-owner child. An external companion at the
supplied or resolved input is diagnosed as unsupported and retained. Compiler
members, JDK binaries and runtime directories never enter this runtime copy.
These runtime sources and tests remain unexecuted; they do not seal an artifact.

The captain must release the isolated POSIX build after inspecting this source
checkpoint. Desktop Java, javac, readiness, installer and TLC launches remain
held. An unbuilt lock, missing helper or changed helper is a failure. Payload
manifest updates are deferred until the artifact and source-to-artifact receipt
are sealed; do not regenerate expected hashes from unreviewed drift.
