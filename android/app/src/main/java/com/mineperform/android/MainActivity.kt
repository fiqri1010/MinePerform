package com.mineperform.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.mineperform.android.data.LocalSqliteAndSyncRepository
import com.mineperform.android.ui.MinePerformApp
import com.mineperform.android.ui.MinePerformTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        val repository = LocalSqliteAndSyncRepository(applicationContext)
        setContent {
            MinePerformTheme {
                MinePerformApp(repository = repository)
            }
        }
    }
}
