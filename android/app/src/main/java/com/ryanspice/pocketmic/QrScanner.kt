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
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage
import java.util.concurrent.Executors

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
    val trimmed = raw.trim()
    val uri = if (trimmed.startsWith("pmic://", ignoreCase = true)) trimmed.substring(7) else trimmed
    val parts = uri.split("/", limit = 2)
    if (parts.size != 2) return null

    val hostPort = parts[0].split(":", limit = 2)
    if (hostPort.isEmpty() || hostPort[0].isBlank()) return null

    val host = hostPort[0].trim()
    if (host.isBlank() || host.any { it.isWhitespace() } || ':' in host || '?' in host || '#' in host) return null
    val port = if (hostPort.size == 1) DEFAULT_PORT else
        hostPort[1].trim().toIntOrNull() ?: return null
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
    val onResult by rememberUpdatedState(onScanned)
    val previewView = remember { PreviewView(context) }
    var error by remember { mutableStateOf<String?>(null) }

    DisposableEffect(lifecycleOwner, previewView) {
        val scanner = BarcodeScanning.getClient(BarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_QR_CODE).build())
        val executor = Executors.newSingleThreadExecutor()
        val mainExecutor = ContextCompat.getMainExecutor(context)
        val cameraProviderFuture = ProcessCameraProvider.getInstance(context)
        val preview = Preview.Builder().build().also { it.surfaceProvider = previewView.surfaceProvider }
        val analysis = ImageAnalysis.Builder()
            .setTargetResolution(Size(1280, 720))
            .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
            .build()
        var disposed = false
        var scanned = false
        var provider: ProcessCameraProvider? = null
        analysis.setAnalyzer(executor) { imageProxy: ImageProxy ->
            val mediaImage = imageProxy.image
            if (mediaImage == null) imageProxy.close()
            else {
                val input = InputImage.fromMediaImage(mediaImage, imageProxy.imageInfo.rotationDegrees)
                val scan = runCatching { scanner.process(input) }.getOrElse {
                    imageProxy.close()
                    return@setAnalyzer
                }
                scan
                    .addOnSuccessListener(mainExecutor) { barcodes ->
                        if (!disposed && !scanned) {
                            val data = barcodes.firstNotNullOfOrNull { barcode ->
                                barcode.rawValue?.let(::parseQrPairingData)
                            }
                            if (data != null) {
                                scanned = true
                                onResult(data)
                            } else if (barcodes.isNotEmpty()) {
                                error = "This QR code is not a valid PocketMic pairing code."
                            }
                        }
                    }
                    .addOnFailureListener(mainExecutor) {
                        if (!disposed) error = "Could not read the QR code. Try again or enter the PC address and key."
                    }
                    .addOnCompleteListener { imageProxy.close() }
            }
        }
        cameraProviderFuture.addListener({
            if (!disposed) {
                try {
                    provider = cameraProviderFuture.get()
                    provider?.bindToLifecycle(lifecycleOwner, CameraSelector.DEFAULT_BACK_CAMERA, preview, analysis)
                } catch (_: Exception) {
                    error = "Could not open the camera. Check camera permission or enter the PC address and key."
                }
            }
        }, mainExecutor)
        onDispose {
            disposed = true
            analysis.clearAnalyzer()
            provider?.unbind(preview, analysis)
            executor.shutdown()
            scanner.close()
        }
    }

    Card(
        colors = CardDefaults.cardColors(containerColor = SurfaceRaised),
        shape = RoundedCornerShape(16.dp),
        modifier = modifier.fillMaxWidth(),
    ) {
        AndroidView(
            factory = { previewView },
            modifier = Modifier
                .fillMaxWidth()
                .height(240.dp),
        )
        Text(
            error ?: "Point camera at QR code on PC receiver",
            color = if (error == null) TextMuted else Danger,
            style = androidx.compose.material3.MaterialTheme.typography.bodySmall,
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 8.dp),
        )
    }
}
