package com.mineperform.android.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

/**
 * Semantic Color & 8pt Spatial Grid Design Tokens matching UI_UX_DESIGN_SYSTEM.md.
 */
object MinePerformTokens {
    val CanvasBg = Color(0xFF0F172A)
    val SurfaceCard = Color(0xFF1E293B)
    val BorderSubtle = Color(0xFF334155)
    val TextPrimary = Color(0xFFF8FAFC)
    val TextSecondary = Color(0xFF94A3B8)

    // 4 Super-Header & Token Palette Accent Colors
    val AccentBlue = Color(0xFF3B82F6)     // INFO DASAR / Column Token
    val AccentAmber = Color(0xFFF59E0B)    // DATA PEMERIKSAAN / Time Op Token
    val AccentEmerald = Color(0xFF10B981)  // PUTUSAN PFPD / Filter Pin / Success
    val AccentViolet = Color(0xFF8B5CF6)   // LINK DATA / Aggregate Token
    val AccentCyan = Color(0xFF06B6D4)     // Reusable Sub-Token
    val AccentPink = Color(0xFFEC4899)     // Logic / Conversion Table
    val AccentRed = Color(0xFFEF4444)      // Anomaly / Danger

    // Strict 8pt Spatial Grid (4/6/8/12/16/24/32/48 dp)
    val Space4 = 4.dp
    val Space6 = 6.dp
    val Space8 = 8.dp
    val Space12 = 12.dp
    val Space16 = 16.dp
    val Space24 = 24.dp
    val Space32 = 32.dp
    val TouchTargetMin = 48.dp
}

private val MinePerformDarkColors = darkColorScheme(
    primary = MinePerformTokens.AccentBlue,
    secondary = MinePerformTokens.AccentEmerald,
    tertiary = MinePerformTokens.AccentViolet,
    background = MinePerformTokens.CanvasBg,
    surface = MinePerformTokens.SurfaceCard,
    onBackground = MinePerformTokens.TextPrimary,
    onSurface = MinePerformTokens.TextPrimary,
    outline = MinePerformTokens.BorderSubtle
)

@Composable
fun MinePerformTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = MinePerformDarkColors,
        content = content
    )
}
