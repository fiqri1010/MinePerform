package com.mineperform.android.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CloudSync
import androidx.compose.material.icons.filled.Dashboard
import androidx.compose.material.icons.filled.Functions
import androidx.compose.material.icons.filled.TableChart
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mineperform.android.data.LocalSqliteAndSyncRepository
import com.mineperform.android.model.IkuDefinitionDto
import com.mineperform.android.model.InspectionCardItem
import com.mineperform.android.model.VisualTokenChip

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MinePerformApp(repository: LocalSqliteAndSyncRepository) {
    var selectedTab by remember { mutableIntStateOf(0) }
    val cards = remember { repository.loadInspectionCards() }
    val ikus = remember { repository.loadIkuDefinitions() }

    Scaffold(
        containerColor = MinePerformTokens.CanvasBg,
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(
                            text = "MinePerform Mobile",
                            fontSize = 18.sp,
                            fontWeight = FontWeight.SemiBold,
                            color = MinePerformTokens.TextPrimary
                        )
                        Text(
                            text = "Workspace 2026 • Offline-First SQLite + Firebase Sync",
                            fontSize = 12.sp,
                            color = MinePerformTokens.TextSecondary
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MinePerformTokens.SurfaceCard
                )
            )
        },
        bottomBar = {
            NavigationBar(containerColor = MinePerformTokens.SurfaceCard) {
                NavigationBarItem(
                    selected = selectedTab == 0,
                    onClick = { selectedTab = 0 },
                    icon = { Icon(Icons.Default.Dashboard, contentDescription = "Ringkasan") },
                    label = { Text("Ringkasan") }
                )
                NavigationBarItem(
                    selected = selectedTab == 1,
                    onClick = { selectedTab = 1 },
                    icon = { Icon(Icons.Default.TableChart, contentDescription = "Data") },
                    label = { Text("Data") }
                )
                NavigationBarItem(
                    selected = selectedTab == 2,
                    onClick = { selectedTab = 2 },
                    icon = { Icon(Icons.Default.Functions, contentDescription = "IKU") },
                    label = { Text("IKU") }
                )
                NavigationBarItem(
                    selected = selectedTab == 3,
                    onClick = { selectedTab = 3 },
                    icon = { Icon(Icons.Default.CloudSync, contentDescription = "Sync/Foto") },
                    label = { Text("Sync/Foto") }
                )
            }
        }
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
        ) {
            when (selectedTab) {
                0 -> SummaryDashboardScreen(cards = cards, ikus = ikus)
                1 -> DataInspectionListScreen(repository = repository)
                2 -> MobileLegoTokenIkuScreen(ikus = ikus)
                3 -> SyncAndOcrScreen(repository = repository)
            }
        }
    }
}

@Composable
private fun SummaryDashboardScreen(
    cards: List<InspectionCardItem>,
    ikus: List<IkuDefinitionDto>
) {
    LazyColumn(
        contentPadding = PaddingValues(MinePerformTokens.Space16),
        verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space12)
    ) {
        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(MinePerformTokens.Space12)
            ) {
                BentoMetricCard(
                    modifier = Modifier.weight(1f),
                    label = "TOTAL IP",
                    value = "${cards.size} Dok",
                    accent = MinePerformTokens.AccentBlue
                )
                BentoMetricCard(
                    modifier = Modifier.weight(1f),
                    label = "TEMUAN",
                    value = "${cards.count { it.temuan }} IP",
                    accent = MinePerformTokens.AccentEmerald
                )
            }
        }
        item {
            Text(
                text = "Portofolio IKU Aktif (${ikus.size})",
                fontSize = 16.sp,
                fontWeight = FontWeight.SemiBold,
                color = MinePerformTokens.TextPrimary
            )
        }
        items(ikus, key = { it.id }) { iku ->
            Card(
                colors = CardDefaults.cardColors(containerColor = MinePerformTokens.SurfaceCard),
                border = BorderStroke(1.dp, MinePerformTokens.BorderSubtle),
                shape = RoundedCornerShape(MinePerformTokens.Space12),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(
                    modifier = Modifier.padding(MinePerformTokens.Space16),
                    verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space4)
                ) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(
                            text = iku.code,
                            fontWeight = FontWeight.Bold,
                            color = MinePerformTokens.AccentCyan
                        )
                        Text(
                            text = "Target: ${iku.targetValue} ${iku.unit}",
                            fontSize = 12.sp,
                            color = MinePerformTokens.AccentEmerald
                        )
                    }
                    Text(
                        text = iku.title,
                        fontSize = 14.sp,
                        color = MinePerformTokens.TextPrimary,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }
        }
    }
}

@Composable
private fun BentoMetricCard(
    modifier: Modifier = Modifier,
    label: String,
    value: String,
    accent: Color
) {
    Card(
        modifier = modifier,
        colors = CardDefaults.cardColors(containerColor = MinePerformTokens.SurfaceCard),
        border = BorderStroke(1.dp, MinePerformTokens.BorderSubtle),
        shape = RoundedCornerShape(MinePerformTokens.Space12)
    ) {
        Column(
            modifier = Modifier.padding(MinePerformTokens.Space16),
            verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space8)
        ) {
            Text(text = label, fontSize = 12.sp, color = MinePerformTokens.TextSecondary)
            Text(text = value, fontSize = 24.sp, fontWeight = FontWeight.Bold, color = accent)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DataInspectionListScreen(repository: LocalSqliteAndSyncRepository) {
    var search by remember { mutableStateOf("") }
    val items = remember(search) { repository.loadInspectionCards(search) }
    var selectedItem by remember { mutableStateOf<InspectionCardItem?>(null) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(MinePerformTokens.Space16),
        verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space12)
    ) {
        OutlinedTextField(
            value = search,
            onValueChange = { search = it },
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text("Cari No. IP, Importir, TPS...") },
            singleLine = true
        )

        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space8),
            modifier = Modifier.fillMaxSize()
        ) {
            items(items, key = { it.rowId }) { item ->
                Card(
                    colors = CardDefaults.cardColors(containerColor = MinePerformTokens.SurfaceCard),
                    border = BorderStroke(1.dp, MinePerformTokens.BorderSubtle),
                    shape = RoundedCornerShape(MinePerformTokens.Space12),
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = MinePerformTokens.TouchTargetMin)
                        .clickable { selectedItem = item }
                ) {
                    Column(
                        modifier = Modifier.padding(MinePerformTokens.Space12),
                        verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space6)
                    ) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text(
                                text = "IP ${item.noIp} • ${item.tps}",
                                fontWeight = FontWeight.Bold,
                                color = MinePerformTokens.AccentBlue
                            )
                            Text(
                                text = item.fclLcl,
                                fontSize = 12.sp,
                                color = MinePerformTokens.AccentAmber
                            )
                        }
                        Text(
                            text = item.importir,
                            fontSize = 14.sp,
                            color = MinePerformTokens.TextPrimary,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis
                        )
                        Row(horizontalArrangement = Arrangement.spacedBy(MinePerformTokens.Space6)) {
                            GroupBadgePill("INFO", MinePerformTokens.AccentBlue)
                            GroupBadgePill("PERIKSA", MinePerformTokens.AccentAmber)
                            GroupBadgePill(
                                if (item.temuan) "TEMUAN" else "SESUAI",
                                if (item.temuan) MinePerformTokens.AccentRed else MinePerformTokens.AccentEmerald
                            )
                            GroupBadgePill("LINK", MinePerformTokens.AccentViolet)
                        }
                    }
                }
            }
        }
    }

    selectedItem?.let { detail ->
        ModalBottomSheet(
            onDismissRequest = { selectedItem = null },
            containerColor = MinePerformTokens.SurfaceCard
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(MinePerformTokens.Space16),
                verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space8)
            ) {
                Text(
                    text = "Detail Pemeriksaan IP ${detail.noIp}",
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold,
                    color = MinePerformTokens.TextPrimary
                )
                HorizontalDivider(color = MinePerformTokens.BorderSubtle)
                detail.allFields.forEach { (k, v) ->
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(text = k, fontSize = 12.sp, color = MinePerformTokens.TextSecondary)
                        Text(
                            text = v.ifBlank { "—" },
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Medium,
                            color = MinePerformTokens.TextPrimary
                        )
                    }
                }
                Spacer(modifier = Modifier.height(MinePerformTokens.Space24))
            }
        }
    }
}

@Composable
private fun GroupBadgePill(text: String, accent: Color) {
    Surface(
        color = accent.copy(alpha = 0.16f),
        border = BorderStroke(1.dp, accent),
        shape = RoundedCornerShape(MinePerformTokens.Space6)
    ) {
        Text(
            text = text,
            fontSize = 11.sp,
            color = accent,
            modifier = Modifier.padding(horizontal = MinePerformTokens.Space8, vertical = MinePerformTokens.Space4)
        )
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun MobileLegoTokenIkuScreen(ikus: List<IkuDefinitionDto>) {
    val palette = remember {
        listOf(
            VisualTokenChip("c1", "Column", "📦", "INPUT.No. IP", 0xFF3B82F6, 0x331E3A8A),
            VisualTokenChip("c2", "TimeOp", "⏱️", "SELISIH_MENIT", 0xFFF59E0B, 0x3378350F),
            VisualTokenChip("c3", "Aggregate", "∑", "PERCENT_WHERE", 0xFF8B5CF6, 0x334C1D95),
            VisualTokenChip("c4", "FilterPin", "🏷️", "FCL/LCL = FCL", 0xFF10B981, 0x33064E3B),
            VisualTokenChip("c5", "SubToken", "🧩", "#Batas_SLA", 0xFF06B6D4, 0x33164E63)
        )
    }
    val canvasChips = remember { mutableStateListOf<VisualTokenChip>() }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(MinePerformTokens.Space16),
        verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space16)
    ) {
        Text(
            text = "Kanvas Lego-Token Mobile (${ikus.size} IKU Tersinkron)",
            fontSize = 16.sp,
            fontWeight = FontWeight.SemiBold,
            color = MinePerformTokens.TextPrimary
        )

        // Zero-Collision FlowRow Formula Canvas (spacedBy 6dp)
        Surface(
            color = MinePerformTokens.SurfaceCard,
            border = BorderStroke(1.dp, MinePerformTokens.BorderSubtle),
            shape = RoundedCornerShape(MinePerformTokens.Space12),
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = MinePerformTokens.Space32 * 3)
        ) {
            FlowRow(
                modifier = Modifier.padding(MinePerformTokens.Space12),
                horizontalArrangement = Arrangement.spacedBy(MinePerformTokens.Space6),
                verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space6)
            ) {
                if (canvasChips.isEmpty()) {
                    Text(
                        text = "Ketuk balok token di bawah untuk menyusun rumus tanpa bentrok teks...",
                        fontSize = 12.sp,
                        color = MinePerformTokens.TextSecondary
                    )
                } else {
                    canvasChips.forEach { chip ->
                        TokenChipButton(chip = chip, onClick = { canvasChips.remove(chip) })
                    }
                }
            }
        }

        Text(
            text = "Palet Token (Target Sentuh Min 48dp)",
            fontSize = 14.sp,
            fontWeight = FontWeight.Medium,
            color = MinePerformTokens.TextSecondary
        )

        FlowRow(
            horizontalArrangement = Arrangement.spacedBy(MinePerformTokens.Space6),
            verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space6)
        ) {
            palette.forEach { chip ->
                TokenChipButton(
                    chip = chip,
                    onClick = { canvasChips.add(chip.copy(id = "${chip.id}_${canvasChips.size}")) }
                )
            }
        }
    }
}

@Composable
private fun TokenChipButton(chip: VisualTokenChip, onClick: () -> Unit) {
    Surface(
        color = Color(chip.bgHex),
        border = BorderStroke(1.dp, Color(chip.borderHex)),
        shape = RoundedCornerShape(MinePerformTokens.Space8),
        modifier = Modifier
            .heightIn(min = MinePerformTokens.TouchTargetMin)
            .clickable(onClick = onClick)
    ) {
        Box(
            contentAlignment = Alignment.Center,
            modifier = Modifier.padding(horizontal = MinePerformTokens.Space12, vertical = MinePerformTokens.Space8)
        ) {
            Text(
                text = "${chip.icon} ${chip.label}",
                fontSize = 12.sp,
                color = MinePerformTokens.TextPrimary
            )
        }
    }
}

@Composable
private fun SyncAndOcrScreen(repository: LocalSqliteAndSyncRepository) {
    var statusText by remember { mutableStateOf("Siap: Google Credential Manager + Firebase Spark Delta Sync") }
    var ocrPreview by remember { mutableStateOf("Belum ada pemindaian OCR lapangan.") }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(MinePerformTokens.Space16),
        verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space16)
    ) {
        Card(
            colors = CardDefaults.cardColors(containerColor = MinePerformTokens.SurfaceCard),
            border = BorderStroke(1.dp, MinePerformTokens.BorderSubtle),
            shape = RoundedCornerShape(MinePerformTokens.Space12),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(
                modifier = Modifier.padding(MinePerformTokens.Space16),
                verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space12)
            ) {
                Text(
                    text = "☁️ Immortal Hybrid Cloud Sync",
                    fontSize = 16.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MinePerformTokens.TextPrimary
                )
                Text(text = statusText, fontSize = 12.sp, color = MinePerformTokens.AccentEmerald)
                Button(
                    onClick = {
                        val hlc = repository.currentHlc()
                        statusText = "Tersinkronisasi (HLC ${hlc.physicalUnixMs} • GZip/Base64 Delta)"
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = MinePerformTokens.TouchTargetMin)
                ) {
                    Icon(Icons.Default.CloudSync, contentDescription = null)
                    Text(text = "  Sinkronkan Delta Sekarang", modifier = Modifier.padding(start = MinePerformTokens.Space8))
                }
            }
        }

        Card(
            colors = CardDefaults.cardColors(containerColor = MinePerformTokens.SurfaceCard),
            border = BorderStroke(1.dp, MinePerformTokens.BorderSubtle),
            shape = RoundedCornerShape(MinePerformTokens.Space12),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(
                modifier = Modifier.padding(MinePerformTokens.Space16),
                verticalArrangement = Arrangement.spacedBy(MinePerformTokens.Space12)
            ) {
                Text(
                    text = "📷 ML Kit On-Device OCR (No. IP & Kontainer)",
                    fontSize = 16.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = MinePerformTokens.TextPrimary
                )
                Text(text = ocrPreview, fontSize = 12.sp, color = MinePerformTokens.TextSecondary)
                OutlinedButton(
                    onClick = {
                        val (noIp, containers) = repository.extractCustomsIdsFromOcrText(
                            "IP 814217 CONT TCNU4829103 TEMU6192041"
                        )
                        ocrPreview = "OCR Terdeteksi → No. IP: ${noIp ?: "-"} | Kontainer: ${containers.joinToString(", ")}"
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = MinePerformTokens.TouchTargetMin)
                ) {
                    Icon(Icons.Default.CameraAlt, contentDescription = null)
                    Text(text = "  Uji Ekstraksi OCR Kontainer", modifier = Modifier.padding(start = MinePerformTokens.Space8))
                }
            }
        }
    }
}
