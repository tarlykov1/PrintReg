const $ = (id) => document.getElementById(id);
let selected = null;
let currentSettings = null;
let searchTimer = null;

function toast(message) {
  $('toast').textContent = message;
  $('toast').classList.remove('hidden');
  setTimeout(() => $('toast').classList.add('hidden'), 3500);
}

async function api(url, options) {
  const response = await fetch(url, { headers: { 'Content-Type': 'application/json' }, ...options });
  const json = await response.json();
  if (!response.ok) throw json;
  return json;
}

function field(obj, camel, pascal) { return obj[camel] ?? obj[pascal]; }
function normalizeSearch(value) { return (value || '').trim().replace(/\s+/g, ' ').replaceAll('ё', 'е').replaceAll('Ё', 'Е').toLowerCase(); }

function appendHighlighted(parent, value, query) {
  const source = value || '';
  const normalizedSource = normalizeSearch(source);
  const normalizedQuery = normalizeSearch(query);
  const index = normalizedSource.indexOf(normalizedQuery);
  if (index < 0) { parent.textContent = source; return; }
  parent.append(document.createTextNode(source.slice(0, index)));
  const mark = document.createElement('mark');
  mark.textContent = source.slice(index, index + query.length);
  parent.append(mark, document.createTextNode(source.slice(index + query.length)));
}

async function refreshStatus() {
  const health = await api('/api/health');
  currentSettings = await api('/api/settings');
  const printing = field(currentSettings, 'printing', 'Printing');
  $('csvStatus').textContent = `CSV: ${health.employees} записей`;
  $('printerStatus').textContent = `Принтер: ${health.printer || 'не выбран'}`;
  $('copies').value = printing.copies ?? printing.Copies ?? 1;
  applyPreviewSettings();
  updatePrintAvailability(Boolean(health.printer));
}

function updatePrintAvailability(hasPrinter) {
  $('printBtn').disabled = !hasPrinter;
  $('printHint').textContent = hasPrinter ? '' : 'Выберите принтер в настройках.';
}

function clearResults() { $('results').replaceChildren(); }

async function search() {
  const query = $('search').value.trim();
  clearResults();
  $('notFound').classList.add('hidden');
  if (query.length < 2) return;

  const rows = await api('/api/employees/search?q=' + encodeURIComponent(query));
  if (!rows.length) { $('notFound').classList.remove('hidden'); return; }

  for (const row of rows) {
    const employee = { fullName: field(row, 'fullName', 'FullName'), position: field(row, 'position', 'Position') };
    const card = document.createElement('div');
    card.className = 'card';
    const text = document.createElement('div');
    const name = document.createElement('b');
    appendHighlighted(name, employee.fullName, query);
    const position = document.createElement('p');
    appendHighlighted(position, employee.position, query);
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = 'Выбрать';
    button.addEventListener('click', () => selectEmployee(employee));
    text.append(name, position);
    card.append(text, button);
    $('results').append(card);
  }
}

function selectEmployee(employee) {
  selected = employee;
  $('previewPanel').classList.remove('hidden');
  $('labelName').textContent = employee.fullName;
  $('labelPosition').textContent = employee.position;
  applyPreviewSettings();
}

function applyPreviewSettings() {
  const printing = currentSettings ? field(currentSettings, 'printing', 'Printing') : null;
  if (!printing) return;
  const label = document.querySelector('.label-preview');
  const orientation = printing.orientation ?? printing.Orientation;
  const margin = printing.marginMm ?? printing.MarginMm ?? 3;
  const nameFont = printing.fullNameFontSize ?? printing.FullNameFontSize ?? 15;
  const positionFont = printing.positionFontSize ?? printing.PositionFontSize ?? 9;
  label.classList.toggle('landscape', orientation === 'Landscape');
  label.style.padding = `${margin * 4}px`;
  $('labelName').style.fontSize = `${Math.max(16, nameFont * 1.7 - (($('labelName').textContent.length || 0) > 32 ? 4 : 0))}px`;
  $('labelPosition').style.fontSize = `${Math.max(12, positionFont * 1.6 - (($('labelPosition').textContent.length || 0) > 40 ? 3 : 0))}px`;
}

async function printSelected() {
  if (!selected) return;
  try {
    const result = await api('/api/print', { method: 'POST', body: JSON.stringify({ ...selected, copies: Number($('copies').value) }) });
    toast(result.message || 'Наклейка отправлена на печать');
  } catch (error) {
    toast(error.message || 'Ошибка печати');
  }
}

async function saveEmployee(printAfter) {
  $('formError').textContent = '';
  const buttons = [$('saveBtn'), $('savePrintBtn')];
  buttons.forEach((button) => { button.disabled = true; });
  try {
    const result = await api('/api/employees', { method: 'POST', body: JSON.stringify({ fullName: $('fullName').value, position: $('position').value }) });
    const employee = field(result, 'data', 'Data');
    closeModal();
    selectEmployee({ fullName: field(employee, 'fullName', 'FullName'), position: field(employee, 'position', 'Position') });
    toast('Сотрудник сохранён');
    await search();
    if (printAfter) await printSelected();
  } catch (error) {
    const duplicate = error.duplicate;
    $('formError').textContent = duplicate ? `${error.message} Уже есть: ${field(duplicate, 'fullName', 'FullName')}` : (error.message || 'Ошибка сохранения');
  } finally {
    buttons.forEach((button) => { button.disabled = false; });
  }
}

function openModal() { $('modal').classList.remove('hidden'); $('fullName').focus(); }
function closeModal() { $('modal').classList.add('hidden'); $('addForm').reset(); $('formError').textContent = ''; }

window.addEventListener('DOMContentLoaded', () => {
  refreshStatus();
  $('search').focus();
  $('search').addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(search, 250); });
  $('printBtn').addEventListener('click', printSelected);
  $('clearBtn').addEventListener('click', () => { $('previewPanel').classList.add('hidden'); selected = null; });
  $('addOpen').addEventListener('click', openModal);
  $('cancelBtn').addEventListener('click', closeModal);
  $('addForm').addEventListener('submit', (event) => { event.preventDefault(); saveEmployee(false); });
  $('savePrintBtn').addEventListener('click', () => saveEmployee(true));
  window.addEventListener('focus', refreshStatus);
});
