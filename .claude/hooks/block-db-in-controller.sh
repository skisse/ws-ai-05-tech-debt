#!/bin/bash
# PreToolUse hook: Blocks direct database calls from the controller layer
# Runs BEFORE Claude writes to file — deterministic, cannot be bypassed

INPUT=$(cat)
TOOL=$(echo "$INPUT" | jq -r '.tool_name // empty')

# Only check Edit and Write
if [[ "$TOOL" != "Edit" && "$TOOL" != "Write" ]]; then
  exit 0
fi

FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
CONTENT=$(echo "$INPUT" | jq -r '.tool_input.new_string // .tool_input.content // empty')

# Only check controller files
if [[ "$FILE" != *Controller* ]]; then
  exit 0
fi

# Check for direct DB patterns
if echo "$CONTENT" | grep -qE '(_dbContext\.|_context\.|DbContext|SqlConnection|SqlCommand|\.Query<|\.Set<|\.FromSql)'; then
  cat <<EOF
╔══════════════════════════════════════════════════════════════╗
║  ARCHITECTURE VIOLATION BLOCKED — Hook: block-db-in-controller ║
╚══════════════════════════════════════════════════════════════╝

File:  $FILE
Rule:  Controllers NEVER call the database directly (see CLAUDE.md)

Detected pattern: direct DbContext/SQL call in controller layer

Layering rule:
  Controller → Service → Repository → Database

Fix:
  1. Move DB logic to IPaymentRepository
  2. Let IPaymentService call the repository
  3. Inject and call service from controller

The hook is deterministic — the change was NOT written to disk.
EOF
  exit 2
fi

exit 0
