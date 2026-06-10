Provide a code review for the given pull request.

To do this, follow these steps precisely:

1. Use a Haiku agent to check if the pull request (a) is closed, (b) is a draft, (c) does not need a code review (eg. because it is an automated pull request, or is very simple and obviously ok), or (d) already has a code review from you from earlier. If so, do not proceed.
2. Use another Haiku agent to give you a list of file paths to (but not the contents of) any relevant CLAUDE.md files from the codebase: the root CLAUDE.md file (if one exists), as well as any CLAUDE.md files in the directories whose files the pull request modified
3. Use a Haiku agent to view the pull request, and ask the agent to return a summary of the change
4. Then, launch 5 parallel Sonnet agents to independently code review the change. The agents should do the following, then return a list of issues and the reason each issue was flagged (eg. CLAUDE.md adherence, bug, historical git context, etc.):
   a. Agent #1: Audit the changes to make sure they comply with the CLAUDE.md. Note that CLAUDE.md is guidance for Claude as it writes code, so not all instructions will be applicable during code review.
   b. Agent #2: Read the file changes in the pull request, then do a shallow scan for obvious bugs. Avoid reading extra context beyond the changes, focusing just on the changes themselves. Focus on large bugs, and avoid small issues and nitpicks. Ignore likely false positives.
   c. Agent #3: Read the git blame and history of the code modified, to identify any bugs in light of that historical context
   d. Agent #4: Read previous pull requests that touched these files, and check for any comments on those pull requests that may also apply to the current pull request.
   e. Agent #5: Read code comments in the modified files, and make sure the changes in the pull request comply with any guidance in the comments.
5. For each issue found in #4, launch a parallel Haiku agent that takes the PR, issue description, and list of CLAUDE.md files (from step 2), and returns a score to indicate the agent's level of confidence for whether the issue is real or false positive. Score on a scale from 0-100:
   a. 0: Not confident at all. False positive that doesn't stand up to light scrutiny, or is a pre-existing issue.
   b. 25: Somewhat confident. Might be real, but may also be a false positive. If stylistic, not explicitly called out in CLAUDE.md.
   c. 50: Moderately confident. Verified real issue, but might be a nitpick. Not very important relative to rest of PR.
   d. 75: Highly confident. Double checked, very likely a real issue hit in practice. Important and directly impacts functionality, or directly mentioned in CLAUDE.md.
   e. 100: Absolutely certain. Confirmed real issue, happens frequently. Evidence directly confirms it.
6. Filter out any issues with a score less than 80. If there are no issues that meet this criteria, do not proceed.
7. Use a Haiku agent to repeat the eligibility check from #1, to make sure the pull request is still eligible for review.
8. Use the gh bash command to comment on the pull request. Keep output brief, avoid emojis, link and cite relevant code files and URLs.

Comment format (3 issues example):

---

### Code review

Found 3 issues:

1. <brief description> (CLAUDE.md says "<...>")

<link: https://github.com/owner/repo/blob/<full-sha>/path/file.cs#L10-L15>

2. <brief description> (CLAUDE.md says "<...>")

<link>

3. <brief description> (bug due to <file and code snippet>)

<link>

🤖 Generated with [Claude Code](https://claude.ai/code)

<sub>- If this code review was useful, please react with 👍. Otherwise, react with 👎.</sub>

---

If no issues found:

---

### Code review

No issues found. Checked for bugs and CLAUDE.md compliance.

🤖 Generated with [Claude Code](https://claude.ai/code)

---

Notes:
- Do not check build signal or attempt to build or typecheck the app
- Use `gh` to interact with GitHub
- Make a todo list first
- Must cite and link each bug with full sha1 and line range (eg. L13-L17)
- Repo name must match the repo being reviewed

Pull request: $ARGUMENTS
