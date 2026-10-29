---- MODULE Safety ----
EXTENDS Naturals
VARIABLE x
Init == x = 0
Step == x < 4 /\ x' = x + 1
Jump == x = 0 /\ x' = 3
Done == x = 4 /\ UNCHANGED x
Next == Step \/ Jump \/ Done
Safe == x <= 2
====
