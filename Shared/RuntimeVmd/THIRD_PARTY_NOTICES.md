# Third-party notices

The native-PMX runtime backend uses `mmd_runtime_ffi.dll` from
`unity-mmd-loader`, copyright 2026 yohawing, under the MIT License.

The bundled `_model.pmx` is the MMD Standard Humanoid Reference Rig from
`VRChat-MMD-PlayMode-Preview`, copyright 2026 MMD Play Mode Preview
contributors, under the Apache License 2.0. It is a reproducibly generated,
mesh-free reference rig containing standard MMD bones, append semantics, leg
and toe IK, and common empty morph definitions.

The target-calibrated Humanoid retargeting path is adapted from the
target-calibration design and implementation in `VRChat-MMD-PlayMode-Preview`,
copyright 2026 MMD Play Mode Preview contributors, under the Apache License
2.0. RuntimeVmd adds a runtime/native-PMX bridge, graceful fallback and
game-compatible lifecycle around that approach.

The complete upstream license and notice texts are distributed in the
`ThirdParty` directory alongside the game Mod.
