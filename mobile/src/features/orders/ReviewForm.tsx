import { useState } from "react"
import { Pressable, StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import { AppText } from "@/components/AppText"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { Icon } from "@/components/Icon"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { sizes, spacing } from "@/theme/tokens"
import { useCreateReview, useOrderReview } from "./queries"

function Stars({ value, onChange }: { value: number; onChange?: (value: number) => void }) {
  return (
    <View
      style={styles.stars}
      accessibilityRole={onChange ? "adjustable" : undefined}
      accessibilityLabel={`${value} em 5 estrelas`}
    >
      {[1, 2, 3, 4, 5].map((star) =>
        onChange ? (
          <Pressable
            key={star}
            onPress={() => onChange(star)}
            accessibilityRole="button"
            accessibilityLabel={`${star} ${star === 1 ? "estrela" : "estrelas"}`}
            accessibilityState={{ selected: star <= value }}
            style={styles.star}
          >
            <Icon name={star <= value ? "star" : "star-border"} size={32} color="warning" />
          </Pressable>
        ) : (
          <Icon key={star} name={star <= value ? "star" : "star-border"} size={20} color="warning" />
        ),
      )}
    </View>
  )
}

// Review of the product and of the experience at the farm after pickup (RF11). The prototype left it for future
// work (section 21); the API supports it, so it sits on the picked-up order.
export function ReviewForm({ orderId, farmId, farmName }: { orderId: string; farmId: string; farmName: string }) {
  const review = useOrderReview(orderId, true)
  const create = useCreateReview(orderId, farmId)
  const snackbar = useSnackbar()
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState("")

  if (review.isPending) return null
  if (review.data) {
    return (
      <Card title="A sua avaliação">
        <Stars value={review.data.rating} />
        {review.data.comment ? <AppText>{review.data.comment}</AppText> : null}
      </Card>
    )
  }

  return (
    <Card title={`Como correu na ${farmName}?`}>
      <AppText variant="caption">Avalie a qualidade dos produtos e o levantamento na quinta.</AppText>
      <Stars value={rating} onChange={setRating} />
      <TextField
        label="Comentário (opcional)"
        value={comment}
        onChangeText={setComment}
        multiline
        maxLength={1000}
        placeholder="O que correu bem? O que podia melhorar?"
      />
      <Button
        label="Enviar avaliação"
        variant="secondary"
        disabled={rating === 0}
        loading={create.isPending}
        onPress={() =>
          create.mutate(
            { rating, comment: comment.trim() || null },
            {
              onSuccess: () => snackbar.show({ message: "Obrigado pela avaliação." }),
              onError: (error) => snackbar.show({ tone: "error", message: errorMessage(error) }),
            },
          )
        }
      />
    </Card>
  )
}

const styles = StyleSheet.create({
  stars: { flexDirection: "row", alignItems: "center", gap: spacing.xxs },
  star: { width: sizes.touch, height: sizes.touch, alignItems: "center", justifyContent: "center" },
})
