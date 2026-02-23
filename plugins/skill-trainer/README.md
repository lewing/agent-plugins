# skill-trainer

Skills for building, testing, and training Copilot CLI skills — patterns, anti-patterns, eval methodology

## Installation

Via marketplace (supports updates):
```
/plugin marketplace add lewing/agent-plugins
/plugin install skill-trainer@lewing-public
/plugin update skill-trainer@lewing-public
```

Or install directly from GitHub:
```
/plugin install lewing/agent-plugins:plugins/skill-trainer
```

## Uninstall

```
/plugin uninstall skill-trainer@lewing-public
```

## Skills

### [skill-builder](skills/skill-builder/SKILL.md)

Build, test, and deploy Copilot CLI skills. Use when creating a new skill, improving an existing skill, testing skills with subagents, deploying skills to a repo, or learning skill development patterns.

**References:**
- [agent-conventions.md](skills/skill-builder/references/agent-conventions.md)
- [anti-patterns.md](skills/skill-builder/references/anti-patterns.md)
- [skill-lifecycle.md](skills/skill-builder/references/skill-lifecycle.md)
- [skill-patterns.md](skills/skill-builder/references/skill-patterns.md)
- [testing-patterns.md](skills/skill-builder/references/testing-patterns.md)

### [skill-trainer-knowledge](skills/skill-trainer-knowledge/SKILL.md)

Institutional knowledge for skill training — frontmatter rules, INVOKES patterns, stop signals, token budgets, anti-patterns, and eval integration. USE FOR: looking up skill-building conventions, understanding training methodology, creating Arena eval requests, checking external reference sources. DO NOT USE FOR: actually training a skill (use SkillTrainer agent), creating new skills from scratch (use skill-builder). INVOKES: no tools — pure knowledge skill.

**References:**
- [eval-integration.md](skills/skill-trainer-knowledge/references/eval-integration.md)
- [skill-builder-knowledge.md](skills/skill-trainer-knowledge/references/skill-builder-knowledge.md)
- [training-methodology.md](skills/skill-trainer-knowledge/references/training-methodology.md)

## Agents

### [SkillTrainer](agents/SkillTrainer.agent.md)

Train and improve Copilot CLI skills through structured eval-driven iteration. USE FOR: improving existing skills, assessing skill quality, creating evals for skills, recording training decisions, opening Arena eval requests. DO NOT USE FOR: creating brand-new skills from scratch (use skill-builder), running Arena evals directly, Squad coordination. INVOKES: skill-builder skill, task tool for multi-model validation, gh CLI for Arena issues.

### [SkillResearcher](agents/SkillResearcher.agent.md)

Validate existing skills against upstream APIs, docs, and reality. USE FOR: fact-checking skill guidance against live docs, detecting stale API assumptions, auditing routing boundaries between related skills, pre-training research. DO NOT USE FOR: training skills (use SkillTrainer), building new skills from scratch (use skill-builder). INVOKES: web_fetch, GitHub MCP tools, Helix/AzDO/maestro MCP tools as needed.
