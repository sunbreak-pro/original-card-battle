export function getSwordsmanTitle(cardTypeCount: number): string {
    if (cardTypeCount >= 50) return "剣神";
    if (cardTypeCount >= 30) return "剣聖";
    if (cardTypeCount >= 15) return "剣豪";
    if (cardTypeCount >= 5) return "剣士";
    return "見習い剣士";
}

export function getMageTitle(cardTypeCount: number): string {
    if (cardTypeCount >= 50) return "魔神";
    if (cardTypeCount >= 30) return "大魔導師";
    if (cardTypeCount >= 15) return "魔導師";
    if (cardTypeCount >= 5) return "魔術士";
    return "見習い魔術士";
}

export function getSummonerTitle(cardTypeCount: number): string {
    if (cardTypeCount >= 50) return "召喚神";
    if (cardTypeCount >= 30) return "召喚師";
    if (cardTypeCount >= 15) return "上級召喚士";
    if (cardTypeCount >= 5) return "召喚士";
    return "見習い召喚士";
}
