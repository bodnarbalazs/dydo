export const title = "Counter safety";
export const state = v => `Count ${v.x}`;
export const step = a => ({ Step: "increment", Jump: "jump", Done: "finished" })[a];
