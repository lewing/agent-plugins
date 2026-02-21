# Agent Conventions

Reference for building GitHub Copilot custom agents (`.agent.md` files). Agents are the sibling of skills — skills provide knowledge/capability, agents provide persona and workflow orchestration.

## Structure

Agents are **flat `.agent.md` files** in `.github/agents/` (repo-level) or `~/.copilot/agents/` (user-level).

> ❌ **NEVER** create subdirectories inside `.github/agents/` with `.md` files — VS Code detects "any `.md` files in `.github/agents/`" and will misidentify them as agents.

If an agent needs reference material, use a **companion knowledge skill** instead of a subdirectory. See [Companion Skills Pattern](#companion-skills-pattern).

## Frontmatter

```yaml
---
name: MyAgent
description: One-line description of what this agent does
tools:
  - tool1
  - tool2
---
```

### Supported Fields

| Field | Type | Notes |
|-------|------|-------|
| `name` | string | Must match filename (e.g., `MyAgent.agent.md` → `name: MyAgent`) |
| `description` | string | Shown in agent picker; use trigger phrases users actually say |
| `tools` | array | **VS Code only** — does NOT control CLI tool availability |
| `agents` | array | Subagents this agent can invoke |
| `model` | string or array | Model preference(s) |
| `handoffs` | array | Agents to hand off to |
| `target` | string | Execution target |
| `mcp-servers` | array | MCP servers to connect |
| `user-invokable` | boolean | Whether users can invoke directly |
| `disable-model-invocation` | boolean | Prevent model from auto-invoking |

> ⚠️ **`tools` field caveat**: This is VS Code-specific. On CLI, tools are inherited from the platform regardless of what's listed. The field may actually **restrict** an agent to only the listed tools in VS Code, preventing CLI tools from being available. Consider omitting it entirely (like Squad does) for cross-platform agents.

## Cross-Platform Tool Naming

Agents may run on CLI or VS Code. Key tool differences:

| Capability | CLI | VS Code |
|-----------|-----|---------|
| Spawn subagent | `task` tool (`agent_type`, `mode`, `model`, `prompt`) | `agent/runSubagent` (prompt only) |
| Read subagent result | `read_agent` tool with `agent_id` | Automatic — results return when subagent completes |
| Model selection per spawn | ✅ `model` parameter | ❌ Not available |
| Fire-and-forget background | ✅ `mode: "background"` | ❌ Not available |
| SQL database | ✅ `sql` tool | ❌ Not available |
| File operations | `create`/`edit`/`view` | Same |

### Platform Detection Pattern (from Squad)

```markdown
**Spawning subagents:**
- If `task` tool is available: use it with `agent_type`, `mode`, `model` parameters → `read_agent` for results
- If `agent/runSubagent` is available: use it — spawn all in one turn, results return automatically
- If neither: work inline (you are the only worker)
```

This pattern lets an agent work on both platforms without hard-coding assumptions.

## Companion Skills Pattern

When an agent needs reference material (methodology docs, templates, domain knowledge):

1. **Don't** put `.md` files in a subdirectory of `.github/agents/`
2. **Do** create a companion skill in `.github/skills/{agent-name}-knowledge/` or `plugins/{group}/skills/{agent-name}-knowledge/`
3. Reference it in the agent body: "Invoke the **{agent-name}-knowledge** skill for templates and reference material"

**Why**: Agent files are distributed as single `.agent.md` files (plugin installers copy only `.agent.md`). Companion skills have their own distribution path and can include multiple files.

Example structure:
```
.github/agents/MyAgent.agent.md          # The agent
.github/skills/my-agent-knowledge/       # Companion skill
  SKILL.md                                # Frontmatter + overview
  references/                             # Deep content
    methodology.md
    templates.md
```

## Plugin Distribution

When agents are distributed via a plugin (`plugin.json`):

```json
{
  "agents": "agents/",           // directory — copies all .agent.md files
  "agents": ["agents/My.agent.md"]  // array — copies specific files
}
```

- Only `.agent.md` files are copied — no companion subdirectories
- Companion skills must be declared separately in the `"skills"` array
- The installer (`blazor-ai.cs` or similar) handles both paths

## Naming Conventions

- Filename: `PascalCase.agent.md` (e.g., `SkillTrainer.agent.md`)
- `name` field: must match filename stem exactly
- Description: use action verbs and user-facing language, not internal jargon

## External References

These docs should be periodically reviewed for updates:

- [VS Code: Custom Agents](https://code.visualstudio.com/docs/copilot/customization/custom-agents)
- [GitHub: About Agents](https://docs.github.com/en/copilot/concepts/agents)
- [VS Code: Agent Skills](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
