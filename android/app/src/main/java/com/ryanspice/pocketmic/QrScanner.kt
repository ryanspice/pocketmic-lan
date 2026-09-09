package com.ryanspice.pocketmic

import android.util.Size
import androidx.annotation.OptIn
import androidx.camera.core.CameraSelector
import androidx.camera.core.ExperimentalGetImage
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage

/**
 * A PocketMic QR code encodes: `pmic://<host>:<port>/<key>`
 *
 * The PC receiver generates this; the phone camera scans it and fills the connection fields.
 * The format is deliberately simple — no base64, no JSON — so it fits in a small QR and is
 * readable by any generic scanner as a plain URI.
 */
data class QrPairingData(
    val host: String,
    val port: Int,
    val pairingKey: String,
)

fun parseQrPairingData(raw: String): QrPairingData? {
    // Accept both "pmic://host:port/key" and plain "host:port/key" for convenience.
    val uri = raw.removePrefix("pmic://")
    val parts = uri.split("/", limit = 2)
    if (parts.size != 2) return null

    val hostPort = parts[0].split(":", limit = 2)
    if (hostPort.isEmpty() || hostPort[0].isBlank()) return null

    val host = hostPort[0].trim()
    val port = hostPort.getOrNull(1)?.trim()?.toIntOrNull() ?: DEFAULT_PORT
    val key = parts[1].trim()

    if (port !in MIN_PORT..MAX_PORT || key.length < MIN_PAIRING_KEY_LENGTH) return null
    return QrPairingData(host, port, key)
}

/**
 * Live camera preview with ML Kit barcode scanning. Calls [onScanned] once with the first
 * valid PocketMic QR code detected, then stops analysis.
 */
@OptIn(ExperimentalGetImage::class)
@Composable
fun QrScannerView(
    onScanned: (QrPairingData) -> Unit,
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val scanner = remember { BarcodeScanning.getClient() }
    val alreadyScanned = remember { mutableSetOf<String>() }

    Card(
        colors = CardDefaults.cardColors(containerColor = SurfaceRaised),
        shape = RoundedCornerShape(16.dp),
        modifier = modifier.fillMaxWidth(),
    ) {
        AndroidView(
            factory = { ctx ->
                val previewView = PreviewView(ctx)
                val cameraProviderFuture = ProcessCameraProvider.getInstance(ctx)
                cameraProviderFuture.addListener({
                    val cameraProvider = cameraProviderFuture.get()
                    val preview = Preview.Builder().build().also {
                        it.surfaceProvider = previewView.surfaceProvider
                    }
                    val analysis = ImageAnalysis.Builder()
                        .setTargetResolution(Size(1280, 720))
                        .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                        .build()
                    analysis.setAnalyzer(ContextCompat.getMainExecutor(ctx)) { imageProxy: ImageProxy ->
                        val mediaImage = imageProxy.image
                        if (mediaImage != null) {
                            val input = InputImage.fromMediaImage(
                                mediaImage,
                                imageProxy.imageInfo.rotationDegrees,
                            )
                            scanner.process(input)
                                .addOnSuccessListener { barcodes ->
                                    for (barcode in barcodes) {
                                        val raw = barcode.rawValue ?: continue
                                        if (raw in alreadyScanned) continue
                                        val data = parseQrPairingData(raw)
                                        if (data != null) {
                                            alreadyScanned.add(raw)
                                            onScanned(data)
                                            break
                                        }
                                    }
                                }
                                .addOnCompleteListener { imageProxy.close() }
                        } else {
                            imageProxy.close()
                        }
                    }
                    try {
                        cameraProvider.unbindAll()
                        cameraProvider.bindToLifecycle(
                            lifecycleOwner,
                            CameraSelector.DEFAULT_BACK_CAMERA,
                            preview,
                            analysis,
                        )
                    } catch (_: Exception) {
                        // Camera unavailable — the user sees a blank preview.
                    }
                }, ContextCompat.getMainExecutor(ctx))
                previewView
            },
            modifier = Modifier
                .fillMaxWidth()
                .height(240.dp),
        )
        Text(
            "Point camera at QR code on PC receiver",
            color = TextMuted,
            style = androidx.compose.material3.MaterialTheme.typography.bodySmall,
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 8.dp),
        )
    }
}
