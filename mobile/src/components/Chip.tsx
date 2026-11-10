import { Pressable, ScrollView, StyleSheet, View } from "react-native"
import { colors, radii, sizes, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon } from "./Icon"

interface ChipProps {
  label: string
  selected?: boolean
  onPress?: () => void
  testID?: string
}

// Filter chip: 36 high inside a 48 touch area; the selected one shows a check besides the green background (section 8).
export function Chip({ label, selected = false, onPress, testID }: ChipProps) {
  return (
    <Pressable
      testID={testID}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      accessibilityLabel={label}
      style={styles.touch}
    >
      {({ pressed }) => (
        <View
          style={[
            styles.chip,
            selected
              ? { backgroundColor: colors.green100, borderColor: colors.green900 }
              : { backgroundColor: pressed ? colors.surface : colors.background, borderColor: colors.outline },
          ]}
        >
          {selected ? <Icon name="check" size={sizes.iconSmall} color="green900" /> : null}
          <AppText variant="label" color={selected ? "green900" : "ink"}>
            {label}
          </AppText>
        </View>
      )}
    </Pressable>
  )
}

interface ChipOption<T extends string> {
  value: T
  label: string
}

interface ChipGroupProps<T extends string> {
  options: ChipOption<T>[]
  value: T
  onChange: (value: T) => void
  scroll?: boolean
  label: string
}

// A row of chips with one selected; scrolls sideways when the options do not fit.
export function ChipGroup<T extends string>({ options, value, onChange, scroll = true, label }: ChipGroupProps<T>) {
  const chips = options.map((option) => (
    <Chip
      key={option.value}
      label={option.label}
      selected={option.value === value}
      onPress={() => onChange(option.value)}
      testID={`chip-${option.value}`}
    />
  ))
  if (!scroll) {
    return (
      <View accessibilityLabel={label} style={styles.wrap}>
        {chips}
      </View>
    )
  }
  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      accessibilityLabel={label}
      contentContainerStyle={styles.row}
      style={styles.scroll}
    >
      {chips}
    </ScrollView>
  )
}

const styles = StyleSheet.create({
  touch: { minHeight: sizes.touch, justifyContent: "center" },
  chip: {
    height: sizes.chip,
    borderRadius: radii.field,
    borderWidth: 1,
    paddingHorizontal: spacing.md,
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.xxs,
  },
  row: { gap: spacing.xs, paddingHorizontal: spacing.md },
  scroll: { flexGrow: 0, marginHorizontal: -spacing.md },
  wrap: { flexDirection: "row", flexWrap: "wrap", gap: spacing.xs },
})
