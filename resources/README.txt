===============================================================================

                            O R D O   C A D E N T I A
                              FRAME PACER · v1.0

                    Makes 30 FPS on a PC feel like 30 FPS
                              on a console.

===============================================================================


WHAT THIS IS
-------------------------------------------------------------------------------

A program that fixes the stuttery feel that 30 FPS has on a computer and
does not have on a console.

You are not imagining it. It is a real problem, with a known cause, and
most of it can be fixed.

It does two things:

  1. Locks your display's refresh rate at whatever you choose, from 60 Hz
     up to the maximum your display supports.

  2. Tunes Windows and the display around one specific game, so that the
     game's frames land on screen at even intervals.


WHY 30 FPS STUTTERS ON A PC AND NOT ON A CONSOLE
-------------------------------------------------------------------------------

A display does not show a picture whenever the game feels like it. It
refreshes on a fixed beat. A 60 Hz display refreshes 60 times per second;
a 165 Hz display refreshes 165 times.

With VSync on, a new frame can only appear on one of those refreshes.
Never in between.

So everything comes down to one simple division:

        display Hz  ÷  frames per second

If the answer is a WHOLE number, every frame waits the same number of
refreshes. The gap between frames is always equal. It is smooth.

If it is NOT a whole number, each frame waits a little, then a little
more. The gap keeps swinging. That swing is the stutter — the "judder".

Here is what happens at 30 FPS:

    Console on a TV:    60 ÷ 30 = 2.0   → 2, 2, 2, 2, 2...
                                          33.3 ms every time. SMOOTH.

    PC at 165 Hz:      165 ÷ 30 = 5.5   → 5, 6, 5, 6, 5, 6...
                                          30.3 ms / 36.4 ms / 30.3 ms...
                                          JUDDER.

That is why a console delivers 30 FPS that looks fine and a PC delivers
30 FPS that looks broken. It is not your graphics card. It is the
division.

The console has the advantage because its TV is 60 Hz and the game is
capped at 30. Two is a whole number. Done.

A 165 Hz display is BETTER than that TV — but 165 is a bad number for 30.

Ordo Cadentia fixes this by putting the display on a rate that divides
evenly (120 Hz, for example: 120 ÷ 30 = 4) while you play, and putting
everything back when you are done.

The BEFORE AND AFTER panel draws this out: each block is one frame, and
the width of the block is how long that frame stays on screen. Blocks of
different widths are the stutter. Equal blocks are the console.


LOCKING THE DISPLAY REFRESH RATE
-------------------------------------------------------------------------------

Section 3 of the main screen scans your display and lists every rate it
supports, from 60 Hz to the maximum. Pick one and click
LOCK THE DISPLAY AT ___ Hz.

What locking does:

  - puts the display on the chosen rate right away;
  - keeps it there. If something changes the rate underneath (a game, the
    graphics driver), the program notices and puts it back;
  - needs no game selected. It is fine just for keeping your display
    where you want it.

What it does NOT do:

  - it is not permanent. The lock holds while the program stays open.
    Close the program and the display goes back to its previous rate on
    its own. That is on purpose: no setting of yours is left changed
    behind your back, and a bad lock undoes itself just by closing the
    program. If you want a permanent rate, change it in Windows Display
    Settings instead.

Rates below 60 Hz are not offered. Some displays expose 48 Hz modes or
lower, and they would only clutter the choice.

If a game in EXCLUSIVE FULLSCREEN insists on changing the rate, the
program tries to put it back three times and then gives up — fighting
would just make the screen flicker forever. Use borderless windowed mode
in that case.

WHO WINS WHEN BOTH WANT THE RATE

The manual lock always wins. If the display is locked and you activate
the program on a game, it respects the lock, leaves the rate alone, and
says so in the log. Deactivating does not release the lock either — only
the UNLOCK button releases it, or closing the program.


THE SIX ADJUSTMENTS
-------------------------------------------------------------------------------

SYNC THE DISPLAY
    The main one. Puts the display on the highest available rate that
    divides evenly into the frames reaching the screen. This is what
    separates stuttering from not stuttering. It is temporary: deactivate
    or close, and the display goes back.

SHARPEN THE TIMER
    Asks Windows for the finest timer it has (0.5 ms instead of 15.6 ms).
    A frame limiter hits 33.33 ms far more precisely when the system
    clock is fine-grained.
    Read the GLOBAL TIMER section below — there is a Windows 10/11 catch
    here.

HIGH PRIORITY
    Puts the game ahead of everything else in the processor queue. Less
    chance of some random program stealing the CPU right when the frame
    was being built.

PERFORMANCE CORES
    Only appears on hybrid processors (Intel 12th generation and newer,
    with P and E cores). Pins the game to the performance cores. When a
    game thread slips onto an efficiency core, the frame arrives late and
    you see the hitch. This stops the slip.

TURN OFF FULLSCREEN OPTIMIZATIONS
    Windows "Fullscreen Optimizations" put an extra layer between the
    game and the screen, and they usually wreck pacing in borderless
    windowed mode. This turns that layer off for that one executable.
    NOTE: it only takes effect the NEXT time the game starts.

QUIET BACKGROUND PROGRAMS
    Lowers the priority of your browser, Discord, Spotify, Steam and the
    like while you play. It closes nothing, and puts everything back to
    normal when you are done.


HOW TO USE IT — STEP BY STEP
-------------------------------------------------------------------------------

1. CAP THE GAME AT THE FPS YOU WANT.

   The program does NOT do this for you (see WHAT IT DOES NOT DO).
   Cap it in one of these places:

     - in the game's own options menu (look for "FPS limit"), or
     - in the NVIDIA / AMD control panel (frame rate limiter), or
     - in RivaTuner (RTSS), which is the most precise of them all.

2. TURN VSYNC ON IN THE GAME.

   Without VSync the frame appears mid-refresh and none of this works.
   If your display has G-Sync or FreeSync, leave it on as well.

3. PICK THE GAME WINDOW.

   Use PICK WINDOW, or POINT to click directly on the game window.

4. IN SECTION 2, ENTER THE SAME FPS YOU CAPPED.

   If you are using frame generation, set the multiplier
   (see the next section — it changes the math).

5. CHECK BEFORE AND AFTER.

   The top strip is how things are now. The bottom one is how they will be.

6. CLICK ACTIVATE.

You can minimize the window and go play — the adjustments stay active.
When you are done, click DEACTIVATE, or just close the program.
Everything goes back to how it was.


FRAME GENERATION (FRAME GEN) — READ THIS
-------------------------------------------------------------------------------

If you use DLSS Frame Generation, FSR Frame Generation or AFMF, the math
CHANGES — and it changes in a way most people get wrong.

The graphics card renders 30 frames, but INVENTS frames in between. What
reaches the display is not 30. With a 2x multiplier, 60 reach it.

    30 rendered  ×  2  =  60 FRAMES ON SCREEN

And the display rate has to match the 60, not the 30:

    No frame gen:   the display must be a multiple of 30  (60, 120...)
    Frame gen 2x:   the display must be a multiple of 60  (60, 120...)
    Frame gen 3x:   the display must be a multiple of 90  (90, 180...)

That is why the multiplier is right there on the main screen: set it
correctly, or the program will work out the wrong rate.

Two honest warnings about frame gen:

  - Frame gen makes the picture smoother, but it does NOT make the game
    more responsive. On a 30 base, the controls still answer like 30.
    Your eyes are fooled; your hands are not.

  - Frame gen needs headroom. If the display is 60 Hz and frame gen is
    producing 60, there is no margin left at all.


GLOBAL TIMER — THE WINDOWS 10/11 CATCH
-------------------------------------------------------------------------------

Up to Windows 10 version 1909, when a program asked for a finer timer,
the WHOLE system got finer. It applied to everyone.

From version 2004 onward, Microsoft changed it: the request now applies
only to the program that asked.

So: Ordo Cadentia asks for 0.5 ms and gets 0.5 ms — but the GAME still
has the coarse clock. When that happens, the SHARPEN THE TIMER adjustment
is marked "partial" and the log says so.

To get the old behaviour back there is a Windows registry key. It needs
administrator rights and a RESTART:

    HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel
    GlobalTimerResolutionRequests  (DWORD) = 1

This is not a secret key or a hack: Microsoft documents it. But it
affects the whole system, so the decision is yours — Ordo Cadentia only
reads that key and reports, it never writes it.

The other five adjustments work fine without it. And the one with the
biggest effect — SYNC THE DISPLAY — does not depend on it at all.


WHAT IT DOES NOT DO
-------------------------------------------------------------------------------

This part matters and deserves plain speaking.

IT DOES NOT CAP YOUR GAME AT 30 FPS.

To force a frame cap on a game, a program has to inject code inside the
game's process — that is how RivaTuner works, and it is exactly the kind
of thing anti-cheat systems read as an attack, which can get an account
banned.

Ordo Cadentia injects nothing into any process. It neither reads nor
writes any game's memory. It only touches SYSTEM things: the display
refresh rate, the timer, process priority and process affinity.

So the division of labour is this:

    YOU cap the game at 30 (in the game, the driver, or RTSS).
    ORDO CADENTIA makes those 30 land evenly on the screen.

The second half is what fixes the stuttery feel. A game capped at 30
without the display rate aligned will still stutter, no matter how good
the limiter is.

It also does not increase FPS, does not improve graphics, and works no
miracles on a game that runs badly for lack of hardware.


QUESTIONS THAT WILL COME UP
-------------------------------------------------------------------------------

"My display has no rate that divides into 30."

    This happens on 144 Hz displays with no 120 or 60 mode
    (144 ÷ 30 = 4.8). In that case the program picks the rate with the
    least swing and warns you it is not perfect. Alternative: play at
    24 FPS (144 ÷ 24 = 6) or 48 (144 ÷ 48 = 3), which divide evenly on
    that display.

"The screen blinks when I activate or lock."

    That is normal. Changing a display's refresh rate always blanks the
    screen for a moment, the same as changing resolution.

"Something crashed and my display was left on the wrong rate."

    Open the program again. It leaves a recovery note behind and puts
    the original rate back by itself on startup.

"The game is in exclusive fullscreen and nothing changes."

    In exclusive fullscreen the game controls the display mode, not
    Windows. Use BORDERLESS WINDOWED mode, or lock the rate BEFORE
    opening the game.

"Do I really need administrator rights?"

    For ordinary games, no. But games with anti-cheat run elevated, and
    without administrator rights the HIGH PRIORITY and PERFORMANCE CORES
    adjustments fail on them. That is why the program asks for elevation
    when it opens. The installer does NOT.

"Can this get me banned?"

    Ordo Cadentia never touches the game's process. It uses the same
    Windows APIs that Task Manager uses to change priority and that the
    Settings panel uses to change the refresh rate. Even so, the final
    word always belongs to each game's anti-cheat.

"My antivirus complained."

    The executables are not digitally signed, and the program does three
    things antivirus software looks at with suspicion: it changes the
    display mode, it changes process priority, and (in the uninstaller's
    case) it deletes itself. None of that is malicious, but the pattern
    resembles it.


WHERE THE FILES LIVE
-------------------------------------------------------------------------------

Installed program:
    %LOCALAPPDATA%\Programs\Ordo Cadentia

Settings and control files, in %LOCALAPPDATA%\OrdoCadentia:

    settings.ini     your choices (FPS, frame gen, rate, adjustments, and
                    the interface language).
                    Deleting this file restores the factory defaults.

    recovery.ini     only exists while the display rate is changed. It is
                    the note that lets the rate be restored if the
                    program dies unexpectedly.

    fullscreen-flags.txt   list of executables the fullscreen-optimizations
                    adjustment touched, so nothing is left behind if the
                    program cannot undo it by itself.


TWO SETTINGS WITH NO BUTTON ON SCREEN
-------------------------------------------------------------------------------

They live in settings.ini because they are a matter of taste and almost
nobody changes them. Close the program before editing, or it will
overwrite the file.

FollowFocus=False

    Set it to True and the display goes back to its normal rate every
    time you leave the game (alt-tab), and returns to the adjusted rate
    when you come back.
    It ships off because every switch blanks the screen for a moment —
    and alt-tabbing a lot turns into a blinking festival.
    It has no effect while the rate is locked by hand.

MinimizeToTray=True

    While the program is active (or the display is locked), minimizing
    hides it next to the clock instead of sending it to the taskbar.
    Set it to False if you would rather it always go to the taskbar.
    With nothing active, minimizing is always normal: the taskbar.


Ordo Cadentia installs no service, installs no driver, creates no
scheduled task, does not start with Windows, and does not access the
internet.


HOW TO UNINSTALL
-------------------------------------------------------------------------------

Any of these:

    - Start menu → Ordo Cadentia → "UNINSTALL Ordo Cadentia"
    - Windows Settings → Apps → Ordo Cadentia → Uninstall
    - The install folder itself → Desinstalar.exe

The uninstaller undoes any flag it left in the registry, deletes the
shortcuts and removes the folder. It asks whether you want to keep your
settings.


===============================================================================
 Ordo Cadentia v1.0 — frames at even intervals, just like a console.
===============================================================================
