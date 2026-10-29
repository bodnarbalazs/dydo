---- MODULE Safety ----
EXTENDS Naturals
VARIABLE x
Init == x = 0
Step == x < 2 /\ x' = x + 1
Jump == FALSE /\ x' = 3
Done == x = 2 /\ UNCHANGED x
Next == Step \/ Jump \/ Done
Safe == x <= 2
====
