package com.idpersonalsecure.app

import android.app.Application
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.provider.OpenableColumns
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewmodel.compose.viewModel
import com.idpersonalsecure.app.data.Document
import com.idpersonalsecure.app.data.DocumentCatalog
import com.idpersonalsecure.app.data.IntegrityException
import com.idpersonalsecure.app.data.VaultRepository
import com.idpersonalsecure.app.share.ShareInfo
import com.idpersonalsecure.app.share.Watermark
import java.io.File
import java.time.Instant
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter

enum class Filter(val label: String) { ALL("Todos"), EXPIRED("Vencidos"), SOON("Próximos"), NO_EXPIRY("Sin vencimiento") }

/** Intención de adjunto que aplica el ViewModel tras guardar. */
sealed interface AttachAction {
    data object Keep : AttachAction
    data object Remove : AttachAction
    class Set(val bytes: ByteArray, val name: String) : AttachAction
}

private fun parseDate(s: String): LocalDate? = try { if (s.isBlank()) null else LocalDate.parse(s) } catch (e: Exception) { null }

class VaultViewModel(app: Application) : AndroidViewModel(app) {
    val repo = VaultRepository(app)
    var unlocked by mutableStateOf(false)
    var error by mutableStateOf<String?>(null)
    var query by mutableStateOf("")
    var filter by mutableStateOf(Filter.ALL)
    var revision by mutableStateOf(0)

    fun unlock(pin: String) {
        if (pin.length < 4) { error = "El PIN debe tener al menos 4 dígitos"; return }
        if (repo.unlock(pin)) { unlocked = true; error = null; revision++ }
        else error = "PIN incorrecto o bóveda corrupta"
    }

    fun lock() { repo.lock(); unlocked = false; query = ""; filter = Filter.ALL }
    fun delete(id: String) { repo.delete(id); revision++ }

    fun save(doc: Document, action: AttachAction) {
        repo.upsert(doc)
        when (action) {
            is AttachAction.Set -> repo.saveAttachment(doc.id, action.bytes)
            AttachAction.Remove -> repo.deleteAttachment(doc.id)
            AttachAction.Keep -> {}
        }
        revision++
    }

    fun visibleDocs(): List<Document> {
        revision // registra dependencia para recomposición
        val today = LocalDate.now()
        return repo.documents.filter { d ->
            val q = query.trim()
            val matches = q.isBlank() || d.name.contains(q, true) ||
                d.number.contains(q, true) || DocumentCatalog.label(d.type).contains(q, true)
            val exp = parseDate(d.expiryDate)
            val passFilter = when (filter) {
                Filter.ALL -> true
                Filter.NO_EXPIRY -> !d.hasExpiry
                Filter.EXPIRED -> d.hasExpiry && exp != null && exp.isBefore(today)
                Filter.SOON -> d.hasExpiry && exp != null && !exp.isBefore(today) && exp.isBefore(today.plusDays(30))
            }
            matches && passFilter
        }.sortedBy { it.name.lowercase() }
    }
}

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: android.os.Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme(colorScheme = if (isSystemInDarkTheme()) darkColorScheme() else lightColorScheme()) {
                Surface(color = MaterialTheme.colorScheme.background) { AppRoot() }
            }
        }
    }
}

@Composable
fun AppRoot(vm: VaultViewModel = viewModel()) {
    if (!vm.unlocked) UnlockScreen(vm) else VaultScreen(vm)
}

@Composable
fun UnlockScreen(vm: VaultViewModel) {
    var pin by remember { mutableStateOf("") }
    val newVault = remember { !vm.repo.vaultExists() }
    Column(
        Modifier.fillMaxSize().padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Text("🛡️ IDPersonalSecure", style = MaterialTheme.typography.headlineMedium)
        Spacer(Modifier.height(8.dp))
        Text(
            if (newVault) "Crea un PIN para tu nueva bóveda" else "Ingresa tu PIN",
            style = MaterialTheme.typography.bodyMedium
        )
        Spacer(Modifier.height(24.dp))
        OutlinedTextField(
            value = pin,
            onValueChange = { if (it.length <= 12 && it.all { c -> c.isDigit() }) pin = it },
            label = { Text("PIN") },
            singleLine = true,
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
            isError = vm.error != null
        )
        vm.error?.let { Spacer(Modifier.height(8.dp)); Text(it, color = MaterialTheme.colorScheme.error) }
        Spacer(Modifier.height(24.dp))
        Button(onClick = { vm.unlock(pin) }, enabled = pin.length >= 4) {
            Text(if (newVault) "Crear bóveda" else "Desbloquear")
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun VaultScreen(vm: VaultViewModel) {
    val context = LocalContext.current
    var editing by remember { mutableStateOf<Document?>(null) }
    var sharing by remember { mutableStateOf<Document?>(null) }
    var showMenu by remember { mutableStateOf(false) }

    val exportLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.CreateDocument("application/octet-stream")
    ) { uri ->
        if (uri != null) runCatching {
            context.contentResolver.openOutputStream(uri)!!.use { vm.repo.export(it) }
        }.onSuccess { toast(context, "Bóveda exportada") }
            .onFailure { toast(context, "Error al exportar: ${it.message}") }
    }

    var pendingImportPin by remember { mutableStateOf<String?>(null) }
    val importLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        val pin = pendingImportPin
        if (uri != null && pin != null) runCatching {
            context.contentResolver.openInputStream(uri)!!.use { vm.repo.import(it, pin) }
        }.onSuccess { vm.revision++; toast(context, "Bóveda importada") }
            .onFailure { toast(context, if (it is IntegrityException) it.message ?: "Integridad inválida" else "Error al importar") }
        pendingImportPin = null
    }
    var askImportPin by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Mi bóveda") },
                actions = {
                    IconButton(onClick = { showMenu = true }) { Icon(Icons.Default.MoreVert, "Menú") }
                    DropdownMenu(expanded = showMenu, onDismissRequest = { showMenu = false }) {
                        DropdownMenuItem(text = { Text("Exportar .securevault") }, onClick = {
                            showMenu = false; exportLauncher.launch("vault-${System.currentTimeMillis()}.securevault")
                        })
                        DropdownMenuItem(text = { Text("Importar .securevault") }, onClick = {
                            showMenu = false; askImportPin = true
                        })
                        DropdownMenuItem(text = { Text("Bloquear") }, onClick = { showMenu = false; vm.lock() })
                    }
                }
            )
        },
        floatingActionButton = {
            FloatingActionButton(onClick = { editing = Document() }) { Icon(Icons.Default.Add, "Agregar") }
        }
    ) { padding ->
        Column(Modifier.padding(padding).fillMaxSize()) {
            OutlinedTextField(
                value = vm.query, onValueChange = { vm.query = it },
                label = { Text("Buscar") }, leadingIcon = { Icon(Icons.Default.Search, null) },
                singleLine = true, modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp)
            )
            Row(Modifier.fillMaxWidth().padding(horizontal = 12.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Filter.values().forEach { f ->
                    FilterChip(selected = vm.filter == f, onClick = { vm.filter = f }, label = { Text(f.label) })
                }
            }
            val docs = vm.visibleDocs()
            if (docs.isEmpty()) {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    Text("No hay documentos.\nToca + para agregar el primero.",
                        style = MaterialTheme.typography.bodyMedium)
                }
            } else {
                LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(16.dp)) {
                    items(docs, key = { it.id }) { doc ->
                        DocumentCard(
                            doc,
                            onEdit = { editing = doc },
                            onDelete = { vm.delete(doc.id) },
                            onShare = {
                                if (!vm.repo.hasAttachment(doc.id) || doc.fileName.isBlank())
                                    toast(context, "Este documento no tiene adjunto (PDF/imagen) para compartir")
                                else sharing = doc
                            }
                        )
                    }
                }
            }
        }
    }

    editing?.let { doc ->
        DocumentEditor(
            doc,
            onDismiss = { editing = null },
            onSave = { d, action -> vm.save(d, action); editing = null }
        )
    }
    sharing?.let { doc ->
        TramiteDialog(onDismiss = { sharing = null }, onConfirm = { tramite ->
            performShare(context, vm.repo, doc, tramite); sharing = null
        })
    }
    if (askImportPin) {
        PinDialog(title = "PIN de la bóveda a importar", onDismiss = { askImportPin = false }) { pin ->
            askImportPin = false; pendingImportPin = pin; importLauncher.launch(arrayOf("*/*"))
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DocumentCard(doc: Document, onEdit: () -> Unit, onDelete: () -> Unit, onShare: () -> Unit) {
    var menu by remember { mutableStateOf(false) }
    ElevatedCard(Modifier.fillMaxWidth().padding(vertical = 6.dp)) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(
                    doc.name.ifBlank { "(sin nombre)" } + if (doc.fileName.isNotBlank()) "  📎" else "",
                    style = MaterialTheme.typography.titleMedium
                )
                Text("${DocumentCatalog.label(doc.type)} · ${doc.country}", style = MaterialTheme.typography.bodySmall)
                if (doc.number.isNotBlank()) Text("N.º ${doc.number}", style = MaterialTheme.typography.bodySmall)
                if (doc.hasExpiry && doc.expiryDate.isNotBlank()) {
                    val exp = parseDate(doc.expiryDate)
                    val vencido = exp != null && exp.isBefore(LocalDate.now())
                    Text(
                        (if (vencido) "Vencido: " else "Vence: ") + doc.expiryDate,
                        style = MaterialTheme.typography.bodySmall,
                        color = if (vencido) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary
                    )
                }
            }
            Box {
                IconButton(onClick = { menu = true }) { Icon(Icons.Default.MoreVert, "Opciones") }
                DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                    DropdownMenuItem(text = { Text("Compartir") }, onClick = { menu = false; onShare() })
                    DropdownMenuItem(text = { Text("Editar") }, onClick = { menu = false; onEdit() })
                    DropdownMenuItem(text = { Text("Borrar") }, onClick = { menu = false; onDelete() })
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DateField(label: String, value: String, onChange: (String) -> Unit) {
    var show by remember { mutableStateOf(false) }
    OutlinedTextField(
        value = value, onValueChange = {}, readOnly = true,
        label = { Text(label) }, modifier = Modifier.fillMaxWidth(),
        trailingIcon = { TextButton(onClick = { show = true }) { Text("📅") } }
    )
    if (show) {
        val initMillis = parseDate(value)?.atStartOfDay(ZoneOffset.UTC)?.toInstant()?.toEpochMilli()
        val state = rememberDatePickerState(initialSelectedDateMillis = initMillis)
        DatePickerDialog(
            onDismissRequest = { show = false },
            confirmButton = {
                TextButton(onClick = {
                    state.selectedDateMillis?.let {
                        onChange(Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate().toString())
                    }
                    show = false
                }) { Text("Aceptar") }
            },
            dismissButton = { TextButton(onClick = { show = false }) { Text("Cancelar") } }
        ) { DatePicker(state = state) }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DocumentEditor(source: Document, onDismiss: () -> Unit, onSave: (Document, AttachAction) -> Unit) {
    val context = LocalContext.current
    var name by remember { mutableStateOf(source.name) }
    var type by remember { mutableStateOf(source.type) }
    var country by remember { mutableStateOf(source.country) }
    var number by remember { mutableStateOf(source.number) }
    var issueDate by remember { mutableStateOf(source.issueDate) }
    var expiryDate by remember { mutableStateOf(source.expiryDate) }
    var hasExpiry by remember { mutableStateOf(source.hasExpiry) }
    var urlSource by remember { mutableStateOf(source.urlSource) }
    var notes by remember { mutableStateOf(source.notes) }
    var typeMenu by remember { mutableStateOf(false) }

    var attachName by remember { mutableStateOf(source.fileName) }
    var pendingBytes by remember { mutableStateOf<ByteArray?>(null) }
    var pendingName by remember { mutableStateOf<String?>(null) }
    var removeFlag by remember { mutableStateOf(false) }

    val pickLauncher = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri != null) {
            val bytes = context.contentResolver.openInputStream(uri)?.use { it.readBytes() }
            val n = queryDisplayName(context, uri) ?: "adjunto"
            if (bytes != null) { pendingBytes = bytes; pendingName = n; removeFlag = false; attachName = n }
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = {
            TextButton(onClick = {
                val action: AttachAction = when {
                    pendingBytes != null -> AttachAction.Set(pendingBytes!!, pendingName!!)
                    removeFlag -> AttachAction.Remove
                    else -> AttachAction.Keep
                }
                val fileName = when {
                    pendingBytes != null -> pendingName!!
                    removeFlag -> ""
                    else -> source.fileName
                }
                onSave(
                    source.copy(
                        name = name, type = type, country = country, number = number,
                        issueDate = issueDate, expiryDate = if (hasExpiry) expiryDate else "",
                        hasExpiry = hasExpiry, urlSource = urlSource, notes = notes, fileName = fileName
                    ),
                    action
                )
            }) { Text("Guardar") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } },
        title = { Text(if (source.name.isBlank()) "Nuevo documento" else "Editar documento") },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState())) {
                OutlinedTextField(name, { name = it }, label = { Text("Nombre") }, singleLine = true, modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(8.dp))
                Box {
                    OutlinedTextField(
                        value = DocumentCatalog.label(type), onValueChange = {}, readOnly = true,
                        label = { Text("Tipo") }, modifier = Modifier.fillMaxWidth(),
                        trailingIcon = { TextButton(onClick = { typeMenu = true }) { Text("▼") } }
                    )
                    DropdownMenu(expanded = typeMenu, onDismissRequest = { typeMenu = false }) {
                        DocumentCatalog.types.forEach { t ->
                            DropdownMenuItem(text = { Text("${t.label} (${t.country})") }, onClick = {
                                type = t.code; if (t.country != "XX") country = t.country; typeMenu = false
                            })
                        }
                    }
                }
                Spacer(Modifier.height(8.dp))
                OutlinedTextField(country, { country = it.uppercase().take(2) }, label = { Text("País (ISO)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(8.dp))
                OutlinedTextField(number, { number = it }, label = { Text("Número") }, singleLine = true, modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(8.dp))
                DateField("Fecha de expedición", issueDate) { issueDate = it }
                Spacer(Modifier.height(8.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Switch(checked = hasExpiry, onCheckedChange = { hasExpiry = it })
                    Spacer(Modifier.width(8.dp)); Text("¿Tiene vencimiento?")
                }
                if (hasExpiry) {
                    DateField("Fecha de vencimiento", expiryDate) { expiryDate = it }
                }
                Spacer(Modifier.height(8.dp))
                Text("Adjunto (PDF o imagen)", style = MaterialTheme.typography.labelMedium)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(attachName.ifBlank { "(ninguno)" }, Modifier.weight(1f), style = MaterialTheme.typography.bodySmall)
                    TextButton(onClick = { pickLauncher.launch(arrayOf("application/pdf", "image/*")) }) { Text("Adjuntar") }
                    if (attachName.isNotBlank())
                        TextButton(onClick = { pendingBytes = null; pendingName = null; removeFlag = true; attachName = "" }) { Text("Quitar") }
                }
                Spacer(Modifier.height(8.dp))
                OutlinedTextField(urlSource, { urlSource = it }, label = { Text("URL fuente (opcional)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(8.dp))
                OutlinedTextField(notes, { notes = it }, label = { Text("Notas") }, modifier = Modifier.fillMaxWidth())
            }
        }
    )
}

@Composable
fun TramiteDialog(onDismiss: () -> Unit, onConfirm: (String) -> Unit) {
    var t by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onClick = { onConfirm(t.trim()) }, enabled = t.isNotBlank()) { Text("Compartir") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } },
        title = { Text("Compartir con marca de agua") },
        text = {
            Column {
                Text("Motivo / trámite (aparecerá en la marca de agua junto a un ID único):",
                    style = MaterialTheme.typography.bodySmall)
                Spacer(Modifier.height(8.dp))
                OutlinedTextField(t, { t = it }, singleLine = true, modifier = Modifier.fillMaxWidth())
            }
        }
    )
}

@Composable
fun PinDialog(title: String, onDismiss: () -> Unit, onConfirm: (String) -> Unit) {
    var pin by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onClick = { onConfirm(pin) }, enabled = pin.length >= 4) { Text("Aceptar") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } },
        title = { Text(title) },
        text = {
            OutlinedTextField(
                value = pin, onValueChange = { if (it.length <= 12 && it.all { c -> c.isDigit() }) pin = it },
                label = { Text("PIN") }, singleLine = true,
                visualTransformation = PasswordVisualTransformation(),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword)
            )
        }
    )
}

private fun performShare(context: Context, repo: VaultRepository, doc: Document, tramite: String) {
    runCatching {
        val bytes = repo.readAttachment(doc.id) ?: throw IllegalStateException("Sin adjunto")
        val info = ShareInfo(tramite, Watermark.newShareId(), currentDateTime())
        val (out, ext) = Watermark.apply(context, bytes, doc.fileName, info)
        val dir = File(context.cacheDir, "shared").apply { mkdirs() }
        val file = File(dir, "${safeName(doc.name)}-${info.shareId}$ext")
        file.writeBytes(out)
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
        val mime = if (ext == ".pdf") "application/pdf" else "image/png"
        val send = Intent(Intent.ACTION_SEND).apply {
            type = mime
            putExtra(Intent.EXTRA_STREAM, uri)
            putExtra(Intent.EXTRA_TEXT, "Documento con marca de agua · ID ${info.shareId} · $tramite")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }
        context.startActivity(Intent.createChooser(send, "Compartir documento"))
        toast(context, "ID de seguimiento: ${info.shareId}")
    }.onFailure { toast(context, "No se pudo compartir: ${it.message}") }
}

private fun currentDateTime(): String =
    LocalDateTime.now().format(DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm"))

private fun safeName(name: String): String =
    Regex("[^\\w\\-]+").replace(name, "_").trim('_').ifBlank { "documento" }

private fun queryDisplayName(context: Context, uri: Uri): String? {
    context.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { c ->
        if (c.moveToFirst()) {
            val i = c.getColumnIndex(OpenableColumns.DISPLAY_NAME)
            if (i >= 0) return c.getString(i)
        }
    }
    return null
}

private fun toast(context: Context, msg: String) =
    Toast.makeText(context, msg, Toast.LENGTH_SHORT).show()
