variable idx
variable BLDC_BASE

( this is a comment )

100 BLDC_BASE !
1 BLDC_BASE @ ! \ motor on

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