import { useEffect, useMemo } from "react";
import type React from "react";
import type { EquipmentSlots } from "@/types/campTypes";
import type {
  InternalPlayerState,
  RuntimeBattleState,
} from "@/contexts/PlayerContext";
import {
  calculateEquipmentAP,
  applyDurabilityDamage,
  distributeApDamageToEquipment,
} from "@/domain/item_equipment/logic/equipmentStats";

type SetPlayerState = React.Dispatch<
  React.SetStateAction<InternalPlayerState>
>;
type SetRuntimeState = React.Dispatch<
  React.SetStateAction<RuntimeBattleState>
>;

export function useEquipmentAP(
  equipmentSlots: EquipmentSlots,
  setPlayerState: SetPlayerState,
  setRuntimeState: SetRuntimeState,
) {
  const equipmentAP = useMemo(
    () => calculateEquipmentAP(equipmentSlots),
    [equipmentSlots],
  );

  // Sync currentAp when equipment changes
  useEffect(() => {
    setRuntimeState((prev) => ({
      ...prev,
      currentAp: equipmentAP.totalAP,
    }));
  }, [equipmentAP.totalAP, setRuntimeState]);

  const applyEquipmentDurabilityDamage = (
    apDamage: number,
  ): { currentAp: number; maxAp: number } => {
    if (apDamage <= 0) {
      const ap = calculateEquipmentAP(equipmentSlots);
      return { currentAp: ap.totalAP, maxAp: ap.maxAP };
    }

    const durabilityDamage = distributeApDamageToEquipment(
      apDamage,
      equipmentSlots,
    );
    const updatedSlots = applyDurabilityDamage(
      equipmentSlots,
      durabilityDamage,
    );

    setPlayerState((prev) => ({
      ...prev,
      equipmentSlots: updatedSlots,
    }));

    const newAP = calculateEquipmentAP(updatedSlots);

    setRuntimeState((prev) => ({
      ...prev,
      currentAp: newAP.totalAP,
    }));

    return { currentAp: newAP.totalAP, maxAp: newAP.maxAP };
  };

  return {
    equipmentAP,
    applyEquipmentDurabilityDamage,
  };
}
