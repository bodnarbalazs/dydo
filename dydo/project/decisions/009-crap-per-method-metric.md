---
type: decision
status: accepted
date: 2026-03-11
area: project
---

# 009 — CRAP Uses Per-Method Cyclomatic Complexity

CRAP (Change Risk Anti-Patterns) is a per-method metric. The gap_check Cobertura parser must use the highest per-method CC, not the class-level sum.

## Problem

The Cobertura XML parser in `gap_check.py` read the class-level `complexity` attribute, which is the sum of all method CCs. This made CRAP scores impossibly high for classes with many simple methods. A class with 30 methods at CC=3 each would get CC=90, producing CRAP scores that could never pass the T1 threshold of 30 even at 100% coverage.

## Context

Coverlet's Cobertura XML includes complexity at both levels:

```xml
<class name="GuardCommand" complexity="341">
  <methods>
    <method name="HandleEditTool" complexity="12">...</method>
    <method name="HandleSearchTool" complexity="8">...</method>
  </methods>
</class>
```

The bug was inherited from LC's backend Cobertura parser. LC's frontend (LCOV) and microservices (radon) paths already used per-method max CC correctly.

## Decision

Extract per-method complexity from `<methods>` elements and take the max. Fall back to the class-level attribute only when no `<methods>` element exists.

```python
methods_el = cls.find("methods")
if methods_el is not None:
    method_ccs = [float(m.get("complexity", 0)) for m in methods_el.findall("method")]
    complexity = max(method_ccs) if method_ccs else 0.0
else:
    complexity = float(cls.get("complexity", 0))
```

## Impact

GuardCommand CC dropped from 341 (class sum) to 52 (worst method). Many modules that appeared to need refactoring now only need coverage improvements to pass T1.

## Related

- [Architecture](../../understand/architecture.md) — Project structure
