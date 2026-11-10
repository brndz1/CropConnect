// Design tokens of the CropConnect visual system (UI/UX report, section 7). Screens use only these values (RNF07).
import type { TextStyle } from "react-native"

export const colors = {
  // Brand
  green700: "#16722F", // Verde Horta: primary actions, active navigation, links
  green900: "#0F4A1E", // titles and text on green containers
  green100: "#E3F1E5", // containers, selected chips, product backgrounds
  terracotta600: "#B0561B", // baskets and highlights
  terracotta100: "#F8E9DF",
  olive500: "#86901A",
  // Neutrals
  ink: "#1E2621", // main text
  textSecondary: "#56615A",
  border: "#CBD3CD", // decorative dividers only, never a field outline
  background: "#FFFFFF",
  surface: "#F4F6F3",
  outline: "#6F7A73", // field and control outlines (passes 3:1)
  // Semantic: always with an icon and a word, never colour alone (section 8)
  success: "#2E7D32",
  warning: "#F2B705", // fill only, with ink text (8.53:1)
  error: "#B3261E",
  info: "#1F5FA8",
  error100: "#FBE9E7",
  warning100: "#FDF3D0",
  info100: "#E3EDF8",
  white: "#FFFFFF",
  scrim: "rgba(30, 38, 33, 0.72)",
  disabled: "#CDD5CF",
} as const

export type ColorName = keyof typeof colors

// Base unit 8, with a half step of 4 (table 11).
export const spacing = { xxs: 4, xs: 8, sm: 12, md: 16, lg: 24, xl: 32, xxl: 48 } as const

export const radii = { field: 8, card: 12, sheet: 16, pill: 24, round: 999 } as const

export const sizes = {
  touch: 48, // minimum touch target (RNF02)
  chip: 36,
  topBar: 64,
  bottomBar: 64,
  navIndicator: 40,
  counter: 18,
  icon: 24,
  iconSmall: 18,
} as const

export const fonts = {
  heading: "Poppins_700Bold",
  headingSemiBold: "Poppins_600SemiBold",
  headingMedium: "Poppins_500Medium",
  headingRegular: "Poppins_400Regular",
  body: "Lato_400Regular",
  bodyBold: "Lato_700Bold",
} as const

// Type scale in sp, so it follows the system font size (RNF03).
export const typography = {
  h1: { fontFamily: fonts.heading, fontSize: 32, lineHeight: 40, color: colors.ink },
  h2: { fontFamily: fonts.headingSemiBold, fontSize: 24, lineHeight: 32, color: colors.ink },
  h3: { fontFamily: fonts.headingSemiBold, fontSize: 18, lineHeight: 26, color: colors.ink },
  number: { fontFamily: fonts.headingSemiBold, fontSize: 20, lineHeight: 28, color: colors.ink },
  body: { fontFamily: fonts.body, fontSize: 16, lineHeight: 24, color: colors.ink },
  bodyBold: { fontFamily: fonts.bodyBold, fontSize: 16, lineHeight: 24, color: colors.ink },
  caption: { fontFamily: fonts.body, fontSize: 13, lineHeight: 18, color: colors.textSecondary },
  label: { fontFamily: fonts.bodyBold, fontSize: 14, lineHeight: 20, color: colors.ink },
  button: { fontFamily: fonts.headingSemiBold, fontSize: 15, lineHeight: 20 },
  badge: { fontFamily: fonts.headingMedium, fontSize: 13, lineHeight: 18 },
} as const satisfies Record<string, TextStyle>

export type TypographyVariant = keyof typeof typography

// Level 0 is flat with a border; level 1 is the soft shadow of sheets and dialogs.
export const elevation = {
  level1: {
    shadowColor: "#000",
    shadowOpacity: 0.12,
    shadowRadius: 12,
    shadowOffset: { width: 0, height: 4 },
    elevation: 4,
  },
} as const
