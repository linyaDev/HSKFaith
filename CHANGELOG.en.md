# Changelog

## 1.1.0 (2026-06-03)

### Beliefs / Memes
- **Cannibal** moved to the mid tier with reworked faith values (+3/season base, −2 per filled section, −3 per unfilled) — cannibalism now steadily pressures colony faith instead of feeding it.
- **Supremacist** buffs removed entirely: the combat stat bonuses (melee hit chance, shooting accuracy, suppression) and the +10% move-speed "supremacist vigor" hediff are gone. Its faith mechanics (slavery bar) are unchanged.
- **Tunneler**: manual deep-drill mining now grants faith (+5 points per drilled portion).

### Faith from rituals
- Ritual faith is now granted **once per year per ritual pattern** — repeating the same ritual no longer farms faith. (Certainty and the Blindsight bonus still apply every time.)
- A **bad (negative) ritual outcome** now grants no faith at all, without consuming the yearly slot.

### Heretics / other faiths
- Removed all mood debuffs for heretics and other-faith pawns — both the colony-wide penalty for having heretics and the heretic's own "among strangers" penalty.

### Diversity of thought
- Reworked into a meme-driven stance that is always present and **intolerant by default**:
  - Disapproved (mild bigotry) is the universal default for ideoligions without a relevant meme.
  - Neutrality (Standard) is reserved for Nudism, High life and Xenophilia.
  - Tolerance (Approved and above) is available only to Individualist.

### Internal
- Added `About/Manifest.xml` (version 1.1.0).
- Split the ritual dev-points patch into its own file.
