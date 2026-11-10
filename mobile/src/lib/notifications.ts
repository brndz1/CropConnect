import type { AppNotification } from "@/api/types"
import type { IconName } from "@/components/Icon"
import { formatNumber } from "./format"
import { parseWeatherContext } from "./sensors"

// The Notifications service writes its texts in English (they also go by email). The app shows them in European
// Portuguese (RNF09), recognising each template; an unknown text is shown as it came.

export interface LocalizedNotification {
  title: string
  message: string
  icon: IconName
}

const ref = "(#[0-9A-F]{8})"

function translateSensor(message: string) {
  const match =
    /^(\w+) of ([\d.]+)(\S*) from sensor (\S+) is (below the minimum|above the maximum) of ([\d.]+)\S*\.?/.exec(message)
  if (!match) return message
  const [, type, value, unit, sensor, side, limit] = match
  const name = type === "Temperature" ? "Temperatura" : "Humidade"
  const cleanUnit = unit === "%" ? " %" : unit ? ` ${unit}` : ""
  const where = side === "below the minimum" ? "abaixo do mínimo" : "acima do máximo"
  const weather = parseWeatherContext(message)
  return `${name} de ${formatNumber(Number(value))}${cleanUnit} no sensor ${sensor}, ${where} de ${formatNumber(Number(limit))}${cleanUnit}.${weather ? ` ${weather.rain ? "Chuva prevista" : "Sem chuva prevista"} nas próximas ${weather.hours} h.` : ""}`
}

export function localizeNotification(
  notification: Pick<AppNotification, "type" | "title" | "message">,
): LocalizedNotification {
  const { title, message } = notification
  let match: RegExpExecArray | null

  if ((match = new RegExp(`^New order ${ref} at (.+)$`).exec(title))) {
    return {
      title: `Nova encomenda ${match[1]}`,
      message: `Recebeu uma nova reserva na ${match[2]}. O stock fica reservado até confirmar ou cancelar.`,
      icon: "receipt-long",
    }
  }
  if ((match = new RegExp(`^Order ${ref} confirmed$`).exec(title))) {
    const farm = /^(.+) confirmed your order/.exec(message)?.[1] ?? "A quinta"
    return {
      title: `Encomenda ${match[1]} confirmada`,
      message: `${farm} aceitou a sua reserva. Recebe um aviso quando estiver pronta a levantar.`,
      icon: "check",
    }
  }
  if ((match = new RegExp(`^Order ${ref} is ready for pickup$`).exec(title))) {
    const farm = /ready for pickup at (.+?)\. You pay/.exec(message)?.[1] ?? "na quinta"
    return {
      title: `Encomenda ${match[1]} pronta a levantar`,
      message: `Pode levantá-la em ${farm} e pagar em mão. Depois confirme o levantamento na app.`,
      icon: "shopping-bag",
    }
  }
  if ((match = new RegExp(`^Order ${ref} picked up$`).exec(title))) {
    return {
      title: `Encomenda ${match[1]} levantada`,
      message: "O cliente confirmou o levantamento.",
      icon: "task-alt",
    }
  }
  if ((match = new RegExp(`^Order ${ref} cancelled by the customer$`).exec(title))) {
    return {
      title: `Encomenda ${match[1]} cancelada pelo cliente`,
      message: "O stock voltou ao catálogo.",
      icon: "cancel",
    }
  }
  if ((match = new RegExp(`^Order ${ref} cancelled$`).exec(title))) {
    const farm = /^(.+) cancelled your order/.exec(message)?.[1] ?? "A quinta"
    const reason = /Reason: (.+)$/.exec(message)?.[1]
    return {
      title: `Encomenda ${match[1]} cancelada`,
      message: `${farm} cancelou a sua encomenda.${reason ? ` Motivo: ${reason}` : ""}`,
      icon: "cancel",
    }
  }
  if ((match = /^New review for (.+)$/.exec(title))) {
    const stars = /gave order (#[0-9A-F]{8}) (\d) stars?/.exec(message)
    const comment = /Comment: (.+)$/.exec(message)?.[1]
    return {
      title: `Nova avaliação na ${match[1]}`,
      message: stars
        ? `Um cliente deu ${stars[2]} ${stars[2] === "1" ? "estrela" : "estrelas"} à encomenda ${stars[1]}.${comment ? ` «${comment}»` : ""}`
        : message,
      icon: "star",
    }
  }
  if ((match = /^(\w+) alert at (.+)$/.exec(title))) {
    const sensor = match[1] === "Temperature" ? "temperatura" : "humidade"
    return { title: `Alerta de ${sensor} na ${match[2]}`, message: translateSensor(message), icon: "warning" }
  }
  return { title, message, icon: notification.type === "SensorAlert" ? "warning" : "notifications" }
}

// Splits the list into Hoje and Anteriores (C12).
export function groupByDay<T extends Pick<AppNotification, "createdAt">>(items: T[], now = new Date()) {
  const today = now.toDateString()
  return {
    today: items.filter((item) => item.createdAt && new Date(item.createdAt).toDateString() === today),
    earlier: items.filter((item) => !item.createdAt || new Date(item.createdAt).toDateString() !== today),
  }
}
