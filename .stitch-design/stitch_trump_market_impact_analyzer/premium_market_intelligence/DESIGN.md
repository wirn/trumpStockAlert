---
name: Premium Market Intelligence
colors:
  surface: '#0b1326'
  surface-dim: '#0b1326'
  surface-bright: '#31394d'
  surface-container-lowest: '#060e20'
  surface-container-low: '#131b2e'
  surface-container: '#171f33'
  surface-container-high: '#222a3d'
  surface-container-highest: '#2d3449'
  on-surface: '#dae2fd'
  on-surface-variant: '#c2c6d6'
  inverse-surface: '#dae2fd'
  inverse-on-surface: '#283044'
  outline: '#8c909f'
  outline-variant: '#424754'
  surface-tint: '#adc6ff'
  primary: '#adc6ff'
  on-primary: '#002e6a'
  primary-container: '#4d8eff'
  on-primary-container: '#00285d'
  inverse-primary: '#005ac2'
  secondary: '#bcc7de'
  on-secondary: '#263143'
  secondary-container: '#3e495d'
  on-secondary-container: '#aeb9d0'
  tertiary: '#ffb786'
  on-tertiary: '#502400'
  tertiary-container: '#df7412'
  on-tertiary-container: '#461f00'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#d8e2ff'
  primary-fixed-dim: '#adc6ff'
  on-primary-fixed: '#001a42'
  on-primary-fixed-variant: '#004395'
  secondary-fixed: '#d8e3fb'
  secondary-fixed-dim: '#bcc7de'
  on-secondary-fixed: '#111c2d'
  on-secondary-fixed-variant: '#3c475a'
  tertiary-fixed: '#ffdcc6'
  tertiary-fixed-dim: '#ffb786'
  on-tertiary-fixed: '#311400'
  on-tertiary-fixed-variant: '#723600'
  background: '#0b1326'
  on-background: '#dae2fd'
  surface-variant: '#2d3449'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 30px
    fontWeight: '600'
    lineHeight: 38px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: -0.01em
  body-main:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: 0em
  data-mono:
    fontFamily: Space Grotesk
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0.02em
  label-caps:
    fontFamily: Work Sans
    fontSize: 11px
    fontWeight: '600'
    lineHeight: 12px
    letterSpacing: 0.06em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  unit: 4px
  container-padding: 24px
  card-gap: 12px
  widget-internal: 8px
  grid-gutter: 16px
---

## Brand & Style

The design system is engineered for high-stakes financial environments where precision, speed, and clarity are paramount. The brand personality is authoritative and technical, designed to evoke a sense of calm control amidst volatile market data. 

The aesthetic is **Modern Corporate with a Technical edge**, prioritizing data density without sacrificing readability. It draws inspiration from Bloomberg terminals and institutional trading platforms but refines them with modern UI conventions. The visual language utilizes a "dark-room" philosophy—deep backgrounds minimize eye strain during long sessions, while high-contrast typography and subtle glows direct the user's attention to critical market shifts. The emotional response is one of trust and institutional-grade reliability.

## Colors

The palette is anchored in a **Refined Dark Mode** architecture. The base layer uses a deep charcoal (#0F172A) to provide maximum contrast for data points. Secondary surfaces use a slate blue-gray (#1E293B) to define containment and hierarchy.

The primary accent is a sophisticated technical blue (#3B82F6), used sparingly for interactive elements and primary actions. Market impact is communicated through high-chroma semantic colors:
- **Emerald (#10B981)**: Positive movement, gains, and "Buy" signals.
- **Rose (#F43F5E)**: Negative movement, losses, and "Sell" signals.
- **Amber (#F59E0B)**: Neutral volatility, warnings, or pending states.
- **Slate (#64748B)**: Inactive or uncertain data points.

## Typography

This design system employs a dual-font strategy to balance editorial clarity with technical precision. 

**Inter** serves as the primary typeface for all UI labels, body text, and headlines, chosen for its exceptional legibility in dark mode and systematic feel. For tabular data, tickers, and log panels, **Space Grotesk** (or a monospaced alternative) is used to ensure numerical alignment and a "terminal" aesthetic. 

Hierarchy is established through weight and case rather than excessive size shifts. Small caps are used for secondary category labels to maintain a compact footprint in data-dense views.

## Layout & Spacing

The layout follows a **Fixed-Fluid Hybrid Grid**. Main dashboard containers adhere to a 12-column grid with tight 16px gutters to maximize screen real estate. 

A strict 4px spacing scale is enforced. In this design system, density is a feature, not a bug. Elements are grouped tightly to allow users to scan vast amounts of information without scrolling. Standardized internal padding for widgets is set to 12px or 16px, ensuring that even with high density, the interface remains legible and structured.

## Elevation & Depth

Depth is conveyed through **Tonal Layering and Low-Contrast Outlines** rather than traditional shadows. This maintains the "technical" feel of a high-end instrument.

- **Level 0 (Background):** #0F172A - The infinite canvas.
- **Level 1 (Cards/Panels):** #1E293B - Distinct data containers.
- **Level 2 (Modals/Popovers):** #334155 - Elevated interaction layers.

Borders are critical: every container uses a subtle 1px stroke (#334155 or #1E293B at 50% opacity). For high-impact scores or critical alerts, a **Subtle Glow** effect is applied using a 10px-15px blur of the semantic color at 20% opacity, creating a "lit from within" appearance that highlights vital data.

## Shapes

The shape language is **Precision-Focused**. A "Soft" rounding (4px) is the standard for cards and buttons, providing just enough modern refinement to avoid the harshness of 0px corners while maintaining a professional, serious tone.

Status badges and small tags use a slightly more rounded profile (full pill) to differentiate them from structural layout elements like cards or input fields.

## Components

- **Buttons:** Primary buttons use the accent blue (#3B82F6) with white text. Secondary buttons are ghost-style with a subtle slate border. Interaction states are indicated by a slight brightness increase rather than a color shift.
- **Data-Dense Cards:** Feature a top-aligned header, a 1px border, and no shadow. Content is subdivided by subtle horizontal hair-lines (#1E293B).
- **Terminal Panels:** Used for audit logs and system messages. Background is slightly darker than the main surface with a monospaced font in light gray or emerald.
- **Statistics Widgets:** Compact blocks featuring a large "Data Mono" value, a small trend Sparkline, and a semantic status badge (Emerald/Rose).
- **Status Badges:** Small, high-contrast pills. For "Positive" indicators, use a dark emerald background with a bright emerald text to ensure readability without being overwhelming.
- **Inputs:** Minimalist fields with 1px borders. Focus states use the primary blue for the border and a very faint blue outer glow.