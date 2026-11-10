import { useState } from "react"
import { StyleSheet, View } from "react-native"
import Svg, { Circle, G, Line, Polygon, Polyline, Rect, Text as SvgText } from "react-native-svg"
import type { Threshold } from "@/api/types"
import { formatNumber } from "@/lib/format"
import { outOfRange, type Point } from "@/lib/sensors"
import { colors, fonts } from "@/theme/tokens"

interface LineChartProps {
  points: Point[]
  threshold?: Pick<Threshold, "minValue" | "maxValue" | "unit">
  // Small chart of a sensor card, without axes.
  compact?: boolean
  height?: number
  accessibilityLabel: string
}

const PAD = { top: 16, right: 12, bottom: 24, left: 36 }

// Line chart of readings. The limits are dashed lines with a label and the points outside them are triangles, so
// the chart never depends on colour alone (report section 8).
export function LineChart({ points, threshold, compact = false, height = 200, accessibilityLabel }: LineChartProps) {
  const [width, setWidth] = useState(0)
  const values = points.map((point) => point.value).filter((value): value is number => value !== null)
  const pad = compact ? { top: 6, right: 6, bottom: 6, left: 6 } : PAD

  let chart = null
  if (width > 0 && values.length > 0) {
    const bounds = threshold ? [threshold.minValue, threshold.maxValue] : []
    const low = Math.min(...values, ...bounds)
    const high = Math.max(...values, ...bounds)
    const span = high - low || 1
    const min = compact ? low - span * 0.15 : Math.min(0, low - span * 0.1)
    const max = high + span * 0.15
    const innerWidth = width - pad.left - pad.right
    const innerHeight = height - pad.top - pad.bottom
    const x = (index: number) =>
      pad.left + (points.length <= 1 ? innerWidth / 2 : (index / (points.length - 1)) * innerWidth)
    const y = (value: number) => pad.top + (1 - (value - min) / (max - min)) * innerHeight
    const drawn = points.map((point, index) =>
      point.value === null ? null : { x: x(index), y: y(point.value), ...point },
    )
    const line = drawn.filter((point): point is NonNullable<typeof point> => point !== null)
    const last = line[line.length - 1]
    const lastOut = last && last.value !== null && outOfRange(last.value, threshold)
    const stroke = compact ? (lastOut ? colors.error : colors.ink) : colors.green700
    // The compact chart shows only the limit nearest to the latest reading.
    const lastValue = last?.value ?? null
    const lastAbove = threshold !== undefined && lastValue !== null && lastValue > threshold.maxValue
    const limits = !threshold
      ? []
      : compact
        ? [lastAbove ? threshold.maxValue : threshold.minValue]
        : [threshold.minValue, threshold.maxValue]

    chart = (
      <Svg width={width} height={height}>
        {threshold && !compact ? (
          <Rect
            x={pad.left}
            y={y(threshold.maxValue)}
            width={innerWidth}
            height={y(threshold.minValue) - y(threshold.maxValue)}
            fill={colors.green100}
            opacity={0.7}
          />
        ) : null}
        {!compact ? (
          <G>
            <Line x1={pad.left} x2={pad.left + innerWidth} y1={y(min)} y2={y(min)} stroke={colors.border} />
            <SvgText
              x={pad.left - 6}
              y={y(min) + 4}
              fontSize={11}
              fill={colors.textSecondary}
              textAnchor="end"
              fontFamily={fonts.body}
            >
              {formatNumber(min)}
            </SvgText>
          </G>
        ) : null}
        {threshold
          ? limits.map((bound) => (
              <G key={bound}>
                <Line
                  x1={pad.left}
                  x2={pad.left + innerWidth}
                  y1={y(bound)}
                  y2={y(bound)}
                  stroke={colors.outline}
                  strokeDasharray="4 4"
                  strokeWidth={1}
                />
                {!compact ? (
                  <>
                    <SvgText
                      x={pad.left - 6}
                      y={y(bound) + 4}
                      fontSize={11}
                      fill={colors.textSecondary}
                      textAnchor="end"
                      fontFamily={fonts.body}
                    >
                      {formatNumber(bound)}
                    </SvgText>
                    <SvgText
                      x={pad.left + 6}
                      y={y(bound) - 5}
                      fontSize={11}
                      fill={colors.ink}
                      fontFamily={fonts.bodyBold}
                    >
                      {`${bound === threshold.maxValue ? "máx." : "mín."} ${formatNumber(bound)} ${threshold.unit}`}
                    </SvgText>
                  </>
                ) : null}
              </G>
            ))
          : null}
        <Polyline
          points={line.map((point) => `${point.x},${point.y}`).join(" ")}
          fill="none"
          stroke={stroke}
          strokeWidth={compact ? 1.5 : 2.5}
          strokeLinejoin="round"
        />
        {line.map((point, index) => {
          const out = point.value !== null && outOfRange(point.value, threshold)
          if (compact && index !== line.length - 1) return null
          if (out) {
            const size = compact ? 4 : 6
            return (
              <Polygon
                key={index}
                points={`${point.x},${point.y - size} ${point.x - size},${point.y + size} ${point.x + size},${point.y + size}`}
                fill={colors.error}
              />
            )
          }
          return <Circle key={index} cx={point.x} cy={point.y} r={compact ? 2.5 : 3.5} fill={stroke} />
        })}
        {!compact
          ? points.map((point, index) =>
              index % Math.ceil(points.length / 7) === 0 || index === points.length - 1 ? (
                <SvgText
                  key={index}
                  x={x(index)}
                  y={height - 6}
                  fontSize={11}
                  fill={colors.textSecondary}
                  textAnchor="middle"
                  fontFamily={fonts.body}
                >
                  {point.label}
                </SvgText>
              ) : null,
            )
          : null}
      </Svg>
    )
  }

  return (
    <View
      style={[styles.box, { height }]}
      onLayout={(event) => setWidth(event.nativeEvent.layout.width)}
      accessible
      accessibilityRole="image"
      accessibilityLabel={accessibilityLabel}
    >
      {chart}
    </View>
  )
}

const styles = StyleSheet.create({ box: { width: "100%" } })
