# Read

The first phase on every area, and the only one on a prose area. Read hunts what neither a test
nor a drive would catch, and tags for the later phases what they can prove. You change nothing.

## Discovery

1. **Pin the area.** In the worktree your brief names, at the audit SHA, open every file in the
   area's map row, whole, not only the diff hunks; `git diff <landing base>..<audit SHA> -- <paths>` shows what landed. Done
   when every path in the row is open at the audit SHA.
2. **Find the intent.** Read the ticket contracts, plan sections and Decision Records the brief
   names for this area, and any Decision Record the area's files cite. Done when you can say in
   one sentence what the area does and which intent governs it.

## Work

3. **Ask every question of every file**, relentlessly:
   - **Intent:** does it do what its ticket asked, all of it, and nothing it was not asked?
   - **Decisions:** does it contradict a Decision Record, the plan or a reference doc?
   - **Comments and docs:** is a comment, help text, message or doc wrong, stale or misleading?
   - **Standards:** does it break `dydo/guides/coding-standards.md` or the code around it?
   - **Security:** boundary validation, injection, path traversal, secrets, authorization,
     unsafe deserialization.
   - **Dead code:** an unused member, export or field, a path nothing reaches, a retirement left
     half-done.
   - **Seams:** in a seam area, what each side assumes of the other, and whether both still hold.

   In a prose area the questions are intent, decisions and contradiction: two passages, in the area
   or across it, that claim different things about one fact. Done when each question has been
   asked of each file.
4. **Prove what reading proves.** Quote both passages at `file:line` for an intent, decision,
   standard, comment or prose finding. For an unused member, run the reference search over the
   whole repository at the audit SHA and quote the command and its output. Done when each such
   finding carries its proof or is dropped as refuted.
5. **Tag the rest.** A failure a test could decide is `testable`; one the running app could show
   is `drivable`; one can be both. Write the input, state and expected observation that would
   decide it. Done when every unproved finding is tagged.

## Document

6. **Write the area's entries** in the return shape. A tagged finding carries the severity it
   would have if proved, and its proof field reads `tagged testable` or `tagged drivable` with the
   deciding input and observation. A known bug in the area's register entries is a sighting. Done
   when every finding of the area is a finding or a sighting line, or the area reports `none`.
