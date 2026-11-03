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

`build-lock.json` binds the captain-inspected **sealed-build** artifact from
[B1c](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031c/result.md).
The 1,825-byte jar has SHA256
`1772e71e2d82f470f115fa7e6d63205c388f84a0fa55537f120b9d53f3d3fbfa`.
This distribution binding is built-not-runtime-validated. `build.py` accepts only absolute, disjoint owned paths on Linux and
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
companion is supplied. B1c ran 21 helper tests successfully, then one compiler
stage with two fresh warning-clean release11 builds and deterministic packaging.
Both jars matched, with exactly the two major55 classes and no manifest or nested
jar. The build-only JDK archive was reverified and reused in contained Linux.
No Java product source, compiler recipe, dependency version or warning policy changed.

The immutable [B1c build receipt](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031c/build-receipt.json)
has SHA256 `60498308cfb5290f093a453e0ab20f4378ed9d35e1624304aa8d75ddad12c8ac`.
It binds the pre-sealing lock SHA256
`ab5d4052b97eb06e43d050ded02bac16b47f87421c44e154228d974c3e86a028`.
The canonical lock now records the inspected artifact, size, receipt identity and
verified build-JDK facts. Its hash changes for this explicit sealing delta;
the historical build receipt is unchanged. The
[binding checkpoint](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/seal-source/checkpoint.md)
retains before/after payload rows and source identities.

This exploded view is compiler-only. Runtime still consumes the byte-identical
official jar. Its manifest dependency requires the separately governed checker
data isolation and precise external-companion diagnostic; compiler representation
does not silently change accepted model-library semantics or close that boundary.

The separately authorized [runtime source checkpoint](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/runtime-isolation/checkpoint.md)
records that boundary's draft implementation. Runtime copies only the original
jar DATA into a new plain registered-owner child. An external companion at the
supplied or resolved input is diagnosed as unsupported and retained. Compiler
members, JDK binaries and runtime directories never enter this runtime copy.
These runtime sources and their tests remain unexecuted. The compiler receipt
does not establish their semantics or cleanup behavior.

The [finite B2 source packet](../../../../../dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b2-source/checkpoint.md)
plans six socket modes and three forwarding controls. It has no execution release.
Desktop Java, javac, readiness, installer and TLC launches remain held. A missing
or changed helper is a failure. No prompt-free Windows, Java assurance, managed
runtime, model, installed-payload or readiness result follows from compilation.
Payload expectations bind only the inspected source closure and explicit sealing
delta; unrelated draft bytes are never adopted automatically.
