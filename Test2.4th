$380 constant BLDC_BASE

variable rotor

: motor_on 1 BLDC_BASE ! ;
: motor_off 0 BLDC_BASE ! ;
: get_rotor_position BLDC_BASE 5 + @ ;

( Main Code Starts Here )

motor_on
0 rotor !
begin
   get_rotor_position rotor @ =
until