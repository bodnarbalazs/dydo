import { resolve, sep } from "node:path";

export function samePath(left, right) { return resolve(left).toLowerCase() === resolve(right).toLowerCase(); }
export function isInside(parent, child) { const resolvedParent = resolve(parent); const resolvedChild = resolve(child); const parentPrefix = resolvedParent.endsWith(sep) ? resolvedParent : `${resolvedParent}${sep}`; return samePath(parent, child) || (resolvedChild.toLowerCase().startsWith(parentPrefix.toLowerCase()) && !resolvedChild.slice(parentPrefix.length).includes(`..${sep}`)); }
