# Cycle

TLC reached 3 states and 3 steps. The chart draws 3 states and 3 arrows.

## Prop breaks
TLC's counterexample is in tlc-Prop.out. It has 3 steps:
0. 0
1. Next: 1
2. Next: 2
3. Next: 0
The run loops through "0", "1" and "2" forever.

## Rules that hold
NoDeadlock
