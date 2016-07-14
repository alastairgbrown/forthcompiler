variable idx
$100 constant BLDC_BASE

( this is a comment )

: motor_on 1 BLDC_BASE ! ;


motor_on
1 idx !
begin
   idx @ 10 <
while
   idx @ 1 + idx !
repeat

idx 10 < if
   222
else
   111
then

$100 allot