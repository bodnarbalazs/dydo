---- MODULE Transport ----
EXTENDS Naturals
VARIABLE x
Init == x = 0
Next == x' = (x + 1) % 4
Spec == Init /\ [][Next]_x /\ WF_x(Next)
TypeOK == x \in 0..3
BelowTwo == x < 2
NeverFour == <> (x = 4)
LimitAction == x' < 2
====
