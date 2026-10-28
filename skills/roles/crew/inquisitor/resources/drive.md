# Drive

The third phase, wherever the landing changed behaviour a user can reach. Drive takes the running
app through the area's journeys and edge cases, the way a user would, so that it works as well as
it reads. You change nothing in the repository; on a project with nothing to run, Drive does not
apply and the area's map row says so.

## Discovery

1. **Load the verification skill** your brief names. Read its launch and drive sections and the
   feature-map files that cover the area. Done when you know how to launch the app at the audit SHA
   and how to drive each feature the area touches.
2. **List the journeys.** Every feature of the area a user can reach through the app or its API,
   each with its edge cases: empty, invalid, boundary, repeated, interrupted, concurrent where a
   user can cause it. Add every register entry for the area tagged `drivable`. Done when every
   feature of the area and every tagged entry has at least one journey on the list.

## Work

3. **Drive each journey** through the verification skill and the API endpoints, following the
   skill's launch model: health-check the instance before trusting it, and clean every drive's
   residue. Capture what you observe: output, screenshot or snapshot, response body, exit code.
   Done when each journey has an observed result.
4. **Judge each journey.** The observed result against the expected one from the intent, the
   feature map and the register entry. Done when each journey is `works`, a finding, a sighting, or
   `unreachable` with its concrete prerequisite.
5. **Name every verification gap.** Where a user can do something the verification skill cannot,
   that is a verification gap: severity `should`, owner `maintain-verification-skill`. Leave the
   verification skill as it is; the gap ticket fixes it. Done when every journey you could not
   drive is a gap or `unreachable` with its prerequisite.

## Document

6. **Write the area's entries** in the return shape. A finding's proof is the rerunnable steps:
   launch, each action or request with its input, then the observed against the expected output.
   A verification gap's `wrong` names the user action and the capability the skill lacks. Each
   unreachable journey goes under `Unreachable` with its prerequisite and the route you tried. Done
   when another agent could rerun every finding and sighting from the return alone.
