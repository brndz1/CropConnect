import { useSession } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Card } from "@/components/Card"
import { Screen } from "@/components/Screen"
import { plural } from "@/lib/format"
import { InfoRow } from "../catalog/InfoRow"
import { useMyOrders } from "../orders/queries"
import { displayName, useMyProfile } from "./queries"

// C16 Privacidade e dados (RGPD): what the platform keeps about the client. The API has no consent settings, data
// export or self-deactivation, so the screen shows the data and explains how deactivation is asked for (RNF10).
export function PrivacyScreen() {
  const session = useSession()
  const profile = useMyProfile()
  const orders = useMyOrders()

  return (
    <Screen title="Privacidade e dados" back="back">
      <AppText color="textSecondary">O que a CropConnect guarda sobre si e para quê (RGPD).</AppText>
      <Card title="Os seus dados">
        <InfoRow icon="email" title={session.email} subtitle="Email da conta, para entrar e receber os avisos" />
        <InfoRow
          icon="person"
          title={displayName(profile.data, session.email)}
          subtitle="Nome, partilhado com a quinta de cada encomenda"
        />
        {profile.data?.phoneNumber ? (
          <InfoRow icon="phone" title={profile.data.phoneNumber} subtitle="Telemóvel (opcional)" />
        ) : null}
        <InfoRow
          icon="receipt-long"
          title={orders.data ? plural(orders.data.length, "encomenda", "encomendas") : "Encomendas"}
          subtitle="Histórico das reservas e das avaliações"
        />
      </Card>
      <Card title="Notificações">
        <AppText>
          Recebe avisos na app e por email quando o estado de uma encomenda muda. Não enviamos publicidade.
        </AppText>
      </Card>
      <Banner
        tone="info"
        title="Desativar a conta"
        message="A desativação é feita pelo administrador da plataforma, a seu pedido. Uma conta desativada deixa de poder entrar; pode pedir a reativação ao suporte."
      />
    </Screen>
  )
}
