# Policy Definitions

This directory contains JSON definitions for built-in action policies, detection policies, and detectors.

## Structure

```
Definitions/
├── Actions/                    # Action policy definitions
│   ├── block-policies.json     # Block action policies
│   ├── throttle-policies.json  # Throttle action policies
│   ├── logonly-policies.json   # LogOnly/shadow policies
│   ├── redirect-policies.json  # Redirect policies
│   ├── challenge-policies.json # Challenge policies
│   └── action-policy.schema.json
├── Detectors/                  # Detector definitions (future)
└── Policies/                   # Detection policy definitions (future)
```

## Inheritance

Policies support inheritance via the `Extends` property:

```json
{
  "block-hard": {
    "Type": "Block",
    "Extends": "block",
    "Description": "Hard block - extends base block policy",
    "StatusCode": 403
  }
}
```

When a policy extends another:

1. All properties from the parent are inherited
2. Child properties override parent properties
3. Multiple levels of inheritance are supported
4. Circular references are detected and rejected

## Logging

When policies are loaded, the inheritance chain is logged:

```
[Information] Loading action policy 'block-hard' (inherits: block)
[Information] Loading action policy 'strict-block' (inherits: block-hard -> block)
```

When policies are executed:

```
[Information] Executing policy 'strict-block' [Block] (chain: strict-block -> block-hard -> block)
```

## Codegen

The JSON files in this directory are:

1. Embedded as resources in the assembly
2. Loaded at startup to create built-in policies
3. Can be overridden by user configuration

Users can extend or override any built-in policy in their appsettings.json:

```json
{
  "BotDetection": {
    "ActionPolicies": {
      "my-custom-block": {
        "Type": "Block",
        "Extends": "block-hard",
        "Description": "My custom block with different message",
        "Message": "Custom access denied message"
      }
    }
  }
}
```

## Schema Validation

Each JSON file can reference the schema for editor support:

```json
{
  "$schema": "./action-policy.schema.json",
  ...
}
```
