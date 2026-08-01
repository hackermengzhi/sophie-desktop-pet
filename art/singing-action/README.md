# Singing action

`sophie-singing.png` is the user-approved four-pose source sheet for Sophie's
singing animation. The production builder removes the magenta key, applies one
shared scale, preserves the left-to-right sequence, and writes the transparent
four-frame row to atlas row 19.

The runtime plays the row once at 500 ms per frame. Sophie then returns to her
normal 20-second idle beat; singing is selected randomly and is not exposed as
a menu action.
