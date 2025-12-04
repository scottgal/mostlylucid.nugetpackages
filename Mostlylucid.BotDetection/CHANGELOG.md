# Changelog

All notable changes to the Mostlylucid.BotDetection package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2024-12-04

### Added
- **Stable release** - Production-ready bot detection middleware
- **Heuristic AI Provider** - Sub-millisecond classification with continuous learning
- **Composable Action Policies** - Separate detection (WHAT) from response (HOW)
- **Multi-signal detection** - User-Agent, headers, IP ranges, behavioral analysis, client-side fingerprinting
- **Stealth responses** - Throttle, challenge, or honeypot bots without revealing detection
- **Auto-updated threat intel** - isbot patterns and cloud IP ranges
- **Full observability** - OpenTelemetry traces and metrics

### Changed
- **Default LLM model** changed from `gemma3:1b` to `gemma3:4b` for better reasoning accuracy
- **Default LLM timeout** increased from 2000ms to 5000ms for larger model
- **Heuristic provider** is now the recommended AI provider (replaces ONNX)
- Simplified LLM prompt to prevent small model hallucinations
- Localhost IP detection improved - no longer incorrectly flagged as datacenter IP

### Removed
- ONNX provider removed in favor of Heuristic provider (faster, no external dependencies)

### Migration Guide
If upgrading from preview versions:
1. Replace `"Provider": "Onnx"` with `"Provider": "Heuristic"` in configuration
2. Update Ollama model if using LLM escalation: `gemma3:4b` recommended
3. Consider increasing `TimeoutMs` to 5000 if using LLM

---

## [0.5.0-preview2] - 2024-11

### Added
- Composable Action Policy System
- Named action policies: block, throttle, challenge, redirect, logonly
- `[BotAction("policy-name")]` attribute for endpoint overrides
- IActionPolicyFactory for configuration-based creation
- IActionPolicyRegistry for runtime policy lookup

---

## [0.5.0-preview1] - 2024-11

### Added
- Policy-Based Detection with named policies
- Path-based resolution with glob patterns
- Built-in policies: default, strict, relaxed, allowVerifiedBots
- Policy transitions based on risk thresholds
- Management endpoints: MapBotPolicyEndpoints()
- `[BotPolicy("strict")]` attribute for controllers
- `[BotDetector("UserAgent,Header")]` for inline detection
- `[SkipBotDetection]` to bypass detection
- Response headers and TagHelpers
- Blackboard architecture with event-driven detection
- Pattern reputation system with time decay
- Fast/Slow path execution model

---

## [0.0.5-preview1] - 2024-10

### Added
- Client-Side Fingerprinting with BotDetectionTagHelper
- Signed token system prevents spoofing
- Headless browser detection
- Inconsistency detection for UA/header mismatches
- RiskBand enum (Low, Elevated, Medium, High)
- Session-level behavioral analysis

---

## [0.0.4-preview1] - 2024-10

### Added
- ONNX-based detection (1-10ms latency)
- Source-generated regex for performance
- OpenTelemetry metrics integration
- YARP reverse proxy integration

---

## [0.0.3-preview2] - 2024-10

### Fixed
- Security fixes (ReDoS, CIDR validation)

---

## [0.0.3-preview1] - 2024-10

### Changed
- Documentation improvements

---

## [0.0.2-preview1] - 2024-10

### Added
- Background updates
- SQLite storage

---

## [0.0.1-preview1] - 2024-10

### Added
- Initial release
- Basic bot detection middleware
- User-Agent analysis
- Header inspection
- IP-based detection
