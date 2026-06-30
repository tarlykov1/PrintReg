const $ = (id) => document.getElementById(id);
let config = null;
let printerList = [];

async function api(url, options) {
  const response = await fetch(url, { headers: { 'Content-Type': 'application/json' }, ...options });
  const json = await response.json();
  if (!response.ok) throw json;
  return json;
}

function field(obj, camel, pascal) { return obj[camel] ?? obj[pascal]; }
function setMessage(message, isError = false) { $('msg').textContent = message; $('msg').className = isError ? 'error' : 'hint'; }

async function loadSettings() {
  config = await api('/api/settings');
  printerList = await api('/api/printers');
  const printing = field(config, 'printing', 'Printing');

  const select = $('printer');
  select.replaceChildren();
  const empty = document.createElement('option');
  empty.value = '';
  empty.textContent = '-- выберите принтер --';
  select.append(empty);
  for (const printer of printerList) {
    const option = document.createElement('option');
    const name = field(printer, 'name', 'Name');
    option.value = name;
    option.textContent = `${name}${field(printer, 'isDefault', 'IsDefault') ? ' (по умолчанию)' : ''}`;
    select.append(option);
  }

  select.value = printing.printerName ?? printing.PrinterName ?? '';
  $('orientation').value = printing.orientation ?? printing.Orientation;
  $('copiesDefault').value = printing.copies ?? printing.Copies;
  $('nameFont').value = printing.fullNameFontSize ?? printing.FullNameFontSize;
  $('posFont').value = printing.positionFontSize ?? printing.PositionFontSize;
  $('margin').value = printing.marginMm ?? printing.MarginMm;
  $('dialog').checked = printing.showPrintDialog ?? printing.ShowPrintDialog;
  updatePrinterInfo();
}

function updatePrinterInfo() {
  const selected = $('printer').value;
  const found = printerList.find((printer) => field(printer, 'name', 'Name') === selected);
  $('printerInfo').textContent = selected ? (found ? `Статус: ${field(found, 'status', 'Status')}` : 'Сохранённый принтер не установлен. Выберите другой.') : 'Принтер не выбран, печать недоступна.';
}

function validate() {
  if (Number($('copiesDefault').value) < 1 || Number($('copiesDefault').value) > 10) return 'Количество копий должно быть от 1 до 10.';
  if (Number($('nameFont').value) < 6 || Number($('nameFont').value) > 40) return 'Размер шрифта ФИО должен быть от 6 до 40.';
  if (Number($('posFont').value) < 6 || Number($('posFont').value) > 30) return 'Размер шрифта должности должен быть от 6 до 30.';
  if (Number($('margin').value) < 0 || Number($('margin').value) > 15) return 'Поля должны быть от 0 до 15 мм.';
  return '';
}

$('settingsForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  const error = validate();
  if (error) { setMessage(error, true); return; }
  config.printing = {
    printerName: $('printer').value,
    labelWidthMm: 40,
    labelHeightMm: 60,
    orientation: $('orientation').value,
    copies: Number($('copiesDefault').value),
    showPrintDialog: $('dialog').checked,
    fullNameFontSize: Number($('nameFont').value),
    positionFontSize: Number($('posFont').value),
    marginMm: Number($('margin').value)
  };
  config.data = field(config, 'data', 'Data');
  config.server = field(config, 'server', 'Server');
  try {
    const result = await api('/api/settings', { method: 'PUT', body: JSON.stringify(config) });
    setMessage(result.message || 'Настройки сохранены.');
  } catch (errorResponse) {
    setMessage(errorResponse.message || 'Ошибка сохранения настроек.', true);
  }
});

$('printer').addEventListener('change', updatePrinterInfo);
$('testPrint').addEventListener('click', async () => {
  try {
    const result = await api('/api/print/test', { method: 'POST', body: '{}' });
    setMessage(result.message || 'Тестовая наклейка отправлена на печать.');
  } catch (error) {
    setMessage(error.message || 'Ошибка печати.', true);
  }
});

window.addEventListener('DOMContentLoaded', loadSettings);
