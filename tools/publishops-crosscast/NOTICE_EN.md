# Third-party notices

[繁體中文](NOTICE.md) · **English**

This is an independent, AI-assisted integration experiment, not an official product of these projects or platforms. Original licence terms remain applicable.

- [bili-cli](https://github.com/kukemc/bili-cli), MIT. Called through `bili-agent==0.7.0`, retaining its login, rate-limiting and audit behaviour.
- [social-auto-upload](https://github.com/dreammis/social-auto-upload), MIT, Copyright 2023 dreammis. Historical optional Douyin form helpers were pinned to `3847fff01f5434a8a32829833163cd6723cabc59`; the upstream LICENSE remains in the separate checkout.
- [Google API Python Client](https://github.com/googleapis/google-api-python-client), Apache-2.0; [google-auth-oauthlib](https://github.com/googleapis/google-auth-library-python-oauthlib), Apache-2.0. Used for the YouTube API.
- [Patchright](https://github.com/Kaliiiiiiiiii-Vinyzu/patchright-python), Apache-2.0. Used by the dedicated Douyin/Instagram/X browser backends; no verification bypass is implemented.

See [pyproject.toml](pyproject.toml) for pinned direct dependencies; each dependency carries its licence.

[Back to the lab](../../README_EN.md)

The 0.3.0 default browser routes do not clone or import social-auto-upload; its old adapter remains for provenance.
