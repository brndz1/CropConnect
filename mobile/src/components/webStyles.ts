import { Platform, type TextStyle } from "react-native"

// Fields draw their own focus border; in the web preview the browser's focus outline is turned off. React Native's
// types do not list "none", which react-native-web accepts.
export const noFocusOutline: TextStyle = Platform.OS === "web" ? ({ outlineStyle: "none" } as unknown as TextStyle) : {}
