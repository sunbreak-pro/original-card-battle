/**
 * BrightnessOverlay
 *
 * Full-screen overlay that controls screen brightness.
 * Uses a dark filter based on settings brightness value.
 * Brightness 0 = normal, 100 = darkest (but not fully black).
 */

import React from 'react';
import { useSettings } from '@/contexts/SettingsContext';
import '@/ui/css/components/BrightnessOverlay.css';

export const BrightnessOverlay: React.FC = () => {
  const { settings } = useSettings();

  // Don't render if brightness is 0 (normal)
  if (settings.brightness === 0) {
    return null;
  }

  // Convert brightness (0-100) to opacity (0-0.6)
  // Max opacity 0.6 to ensure content is always visible
  const opacity = (settings.brightness / 100) * 0.6;

  return (
    <div
      className="brightness-overlay"
      style={{ opacity }}
      aria-hidden="true"
    />
  );
};

export default BrightnessOverlay;
