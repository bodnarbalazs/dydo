import { resolve, sep } from "node:path";

export function samePath(left, right) { return resolve(left).toLowerCase() === resolve(right).toLowerCase(); }
export function isInside(parent, child) { const resolvedParent = resolve(parent); const resolvedChild = resolve(child); return samePath(parent, child) || (resolvedChild.toLowerCase().startsWith(`${resolvedParent.toLowerCase()}${sep}`) && !resolvedChild.slice(resolvedParent.length).includes(`..${sep}`)); }
