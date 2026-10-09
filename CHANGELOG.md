# Changelog

## v0.2.0: changes since 0.1

### Installation

- BepInEx 5 loading, with a ready-to-extract complete archive and a smaller plugin archive.
- A 0.1.x migration script backs up legacy player files and removes old loading registrations.
- Existing dance folders and `.unity3d` dances remain supported, alongside official dance `.me` containers.

### Player

- Redesigned library, import/create and settings pages, plus a mini player.
- Search, folder/format filters, favorites, queues, sequential playback, repeat one and shuffle.
- A favorite button beside the current title; playback controls in both the library and creation pages.
- Separate saved positions for full and mini views; drag empty panel areas or move the character while the panel is open.
- Customizable shortcuts with disable, clear and reset controls, plus separate play/stop and other playback bindings.
- English and Chinese UI, with JSON-based additional languages.

### VMD playback and creation

- `.vmdance` packages for motion, expressions, lip sync, cameras, audio and additional layers; direct `.vmd` previews.
- Embedded expressions and cameras retained from body VMDs, with separate files and folder matching supported.
- Open existing packages, restore fields, save edits with backups, or export copies.
- Free-form multiline credits for asset authors, sources and notes, saved with each package.
- Live audio timing adjustment.
- Per-package foot IK, reference eye height and camera multiplier for adaptation across characters.
- Reworked skeleton adaptation, leg IK and scale handling to improve twisting, hip movement and motion continuity.
- Broader expression mapping for MMD shape keys and VRM expression definitions across multiple meshes.

### Cameras and game controls

- Switch between dance and front cameras, restoring camera state when playback stops.
- Camera adaptation uses the character's original eye height independently of mouse-wheel display scaling; packages without reference metadata use a 1.65 m baseline.
- Improved projection and clipping, including missing character parts in distant shots.
- Native menus, settings and BlendShape editing temporarily take camera control; dance cameras resume afterward.
- Desktop-window following pauses during MMD cameras and returns to its previous setting afterward.
- Global background-shadow control and preserved character display size when switching dances.

### Companion tool

The optional `DanceBundleAudioFixer` is available in the 0.1.2 assets for some old `.unity3d` audio loading stalls. See the [tool guide](docs/user/DANCE_BUNDLE_AUDIO_FIXER.md).

### Thanks

Thanks to [**散歩猫**](https://space.bilibili.com/95425983) for her suggestions and feedback, which helped improve this update.
