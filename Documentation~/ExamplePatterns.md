# TitanTool Example Patterns

Small graphs are easier to learn from than one giant boss graph. These are the package patterns worth keeping as focused examples.

## Move While Shooting

Use **Run Together** with two child branches:

- branch 1: movement node that returns `Running` until the boss reaches a point
- branch 2: shooting or spawn nodes that can finish quickly

Completed branches wait inside the parallel node while unfinished branches keep ticking.

## Repeat Attack Pattern

Use **Repeat Sequence** around a compact group such as:

- shoot
- wait
- shoot
- wait

The repeater runs the connected child branches in order, then repeats the whole group for the configured loop count. Use `After Completion > Remember Success` when the repeated sequence should finish once and not replay the next time its parent reaches it.

## Parallel With Delay

Use **Run Together** when one branch should keep time while another branch does work:

- branch 1: delay
- branch 2: movement, animation, or projectile work

Pick the preset based on whether the group should fail fast or wait for every branch.

## Blackboard Counter Gate

Use runtime values to count attempts or phase steps:

- RuntimeMath adds to a counter
- RuntimeCompare checks the threshold
- RuntimeMath resets the counter after the gate passes

Keep the reset outside the repeated block unless the counter should restart every loop.

## Run Once Intro Attack

Use **Run Once** for a boss intro, roar, first attack, or setup move that should not replay every graph tick.

After the intro succeeds, route into the normal selector, sequence, or phase logic.
