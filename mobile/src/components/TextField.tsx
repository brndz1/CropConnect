import { useState, type ComponentRef, type Ref } from "react"
import { Pressable, StyleSheet, TextInput, View, type TextInputProps } from "react-native"
import { colors, fonts, radii, sizes, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon, type IconName } from "./Icon"
import { noFocusOutline } from "./webStyles"

interface TextFieldProps extends Omit<TextInputProps, "style"> {
  label: string
  icon?: IconName
  error?: string | null
  hint?: string
  // Shows the eye toggle of password fields.
  secure?: boolean
  // Text after the value, such as "€" or "kg".
  suffix?: string
  ref?: Ref<ComponentRef<typeof TextInput>>
}

// Label always visible above; 1 px outline, 2 px green on focus; errors with an icon and a message below (table 12).
export function TextField({
  label,
  icon,
  error,
  hint,
  secure = false,
  suffix,
  editable = true,
  onFocus,
  onBlur,
  ref,
  ...props
}: TextFieldProps) {
  const [focused, setFocused] = useState(false)
  const [hidden, setHidden] = useState(true)
  const borderColor = error ? colors.error : focused ? colors.green700 : colors.outline

  return (
    <View style={styles.wrapper}>
      <AppText variant="label">{label}</AppText>
      <View
        style={[
          styles.field,
          { borderColor, borderWidth: focused || error ? 2 : 1 },
          !editable && { backgroundColor: colors.surface },
        ]}
      >
        {icon ? <Icon name={icon} color="textSecondary" /> : null}
        <TextInput
          ref={ref}
          style={[styles.input, noFocusOutline]}
          placeholderTextColor={colors.textSecondary}
          editable={editable}
          secureTextEntry={secure && hidden}
          accessibilityLabel={label}
          accessibilityHint={error ?? hint}
          maxFontSizeMultiplier={1.5}
          onFocus={(event) => {
            setFocused(true)
            onFocus?.(event)
          }}
          onBlur={(event) => {
            setFocused(false)
            onBlur?.(event)
          }}
          {...props}
        />
        {suffix ? <AppText color="textSecondary">{suffix}</AppText> : null}
        {secure ? (
          <Pressable
            onPress={() => setHidden((value) => !value)}
            accessibilityRole="button"
            accessibilityLabel={hidden ? "Mostrar palavra-passe" : "Ocultar palavra-passe"}
            hitSlop={8}
            style={styles.toggle}
          >
            <Icon name={hidden ? "visibility" : "visibility-off"} color="textSecondary" />
          </Pressable>
        ) : null}
      </View>
      {error ? (
        <View style={styles.message} accessibilityLiveRegion="polite">
          <Icon name="error" size={sizes.iconSmall} color="error" />
          <AppText variant="caption" color="error" style={styles.messageText}>
            {error}
          </AppText>
        </View>
      ) : hint ? (
        <AppText variant="caption">{hint}</AppText>
      ) : null}
    </View>
  )
}

const styles = StyleSheet.create({
  wrapper: { gap: spacing.xs },
  field: {
    minHeight: sizes.touch + 4,
    borderRadius: radii.field,
    paddingHorizontal: spacing.sm,
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.sm,
    backgroundColor: colors.background,
  },
  input: {
    flex: 1,
    minHeight: sizes.touch,
    fontFamily: fonts.body,
    fontSize: 16,
    color: colors.ink,
    paddingVertical: spacing.xs,
  },
  toggle: { padding: spacing.xxs },
  message: { flexDirection: "row", alignItems: "center", gap: spacing.xxs },
  messageText: { flex: 1, fontSize: 14 },
})
