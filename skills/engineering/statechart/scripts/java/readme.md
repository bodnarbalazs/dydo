# Statechart TLC transport

Original MIT statechart code; [LICENSE](LICENSE) is the repository license.
The upstream statechart notice remains in [the skill license](../../LICENSE).
This helper uses public Java APIs; it contains no copied OpenJDK implementation.

`LoopbackTlc` installs `LoopbackSockets` before passing the original argument array
to unchanged TLC 1.7.4. Server sockets bind explicitly to IPv4 `127.0.0.1`.
Client sockets accept only that numeric address or `localhost`, and connect to
the numeric address. Installation, an existing factory, bind and connection
errors propagate; there is no wildcard or raw-TLC fallback. This is local
transport for supported checking, not a sandbox for injected agents or model
extensions. A global factory cannot override a per-object factory. The design
rests on the public
[RMISocketFactory API](https://docs.oracle.com/en/java/javase/21/docs/api/java.rmi/java/rmi/server/RMISocketFactory.html).

## Build

The sources and `build.py` live beside this readme in the dydo repository; an installed
skill ships only the jar, this readme, the license and `build-lock.json`.
The lock pins the two sources, TLC 1.7.4 as compiler input, the build-only Temurin JDK
21.0.8+9 archive and the release-11 compiler and packaging recipe. `build.py` accepts
only absolute, disjoint owned paths on Linux, verifies every input against the lock,
and neither downloads nor installs anything. Two fresh builds must match byte for byte.

The lock's `artifact` binds the shipped 1,825-byte jar, SHA-256
`1772e71e2d82f470f115fa7e6d63205c388f84a0fa55537f120b9d53f3d3fbfa`, with exactly two
release-11 classes. Runtime checks the jar against that identity before every TLC call.

## Status

The jar was built during the Windows-era run; that build and its review are recorded at
[67cd2127](https://github.com/bodnarbalazs/dydo/tree/67cd21278872679398479601b99d95125678b7cd/dydo/project/pitfalls/dyd-415-statechart-evidence).
It is not runtime-validated. It stays only if a Linux check shows TLC's RMI listeners
bound to `127.0.0.1` and the jar rebuilds byte-identically with the pinned JDK.
