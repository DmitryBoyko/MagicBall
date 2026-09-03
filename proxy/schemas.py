from __future__ import annotations

from pydantic import BaseModel, Field


class DeterministicProfile(BaseModel):
    user_name: str = ""
    zodiac_sign: str = ""
    astrological_element: str = ""
    ruling_planet: str = ""
    destiny_number: int = 0
    chinese_totem: str = ""
    age_group: str = ""


class DynamicSnapshot(BaseModel):
    exact_time_context: str = ""
    time_of_day: str = ""
    current_season: str = ""
    geo_location_type: str = ""
    weather_state: str = ""
    device_battery_aura: str = ""
    device_power_state: str = ""
    inquiry_pulse_aura: str = ""
    photo_mystic_tag: str = ""
    photo_color_palette: str = ""
    photo_luminance_vibe: str = ""
    imagenet_raw_tag: str = ""
    entropy_word_anchor: str = ""
    ball_mood_modifier: str = ""
    ball_tint_name: str = ""
    ball_tint_meaning: str = ""
    ball_tint_modifier: str = ""
    world_pressure_modifier: str = ""
    ball_mood_code: int = 0
    world_pressure_code: int = 0


class OracleIn(BaseModel):
    deterministic_profile: DeterministicProfile = Field(default_factory=DeterministicProfile)
    dynamic_snapshot: DynamicSnapshot = Field(default_factory=DynamicSnapshot)


class OracleOut(BaseModel):
    interpretation: str
    summary: str = ""
    osiris_present: bool = False
    source: str = "synthesized"
    ai_model: str | None = None
    fallback_used: bool = False
    fallback_reason: str | None = None
    similarity: float = 0.0
