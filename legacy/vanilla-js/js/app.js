document.addEventListener("DOMContentLoaded", () => {
activateCurrentNavLink();
setupQuotationModule();
setupInventoryModule();
setupPurchaseModule();
});

function activateCurrentNavLink() {
document.querySelectorAll(".navbar a").forEach((link) => {
const linkPath = new URL(link.href).pathname;
const currentPath = window.location.pathname;

    if (linkPath === currentPath) {
        link.classList.add("active-link");
    }
});

}

function setupQuotationModule() {
const addLineBtn = document.getElementById("add-line-btn");

if (!addLineBtn) {
    return;
}

const state = {
    lines: [],
    charges: []
};

const elements = {
    quoteNumber: document.getElementById("quote-number"),
    quoteClient: document.getElementById("quote-client"),
    quotePhone: document.getElementById("quote-phone"),
    documentType: document.getElementById("document-type"),
    quoteDate: document.getElementById("quote-date"),
    quoteValidity: document.getElementById("quote-validity"),
    currency: document.getElementById("quote-currency"),

    productCode: document.getElementById("product-code"),
    cabysCode: document.getElementById("cabys-code"),
    unit: document.getElementById("unit"),
    quantity: document.getElementById("quantity"),
    unitPrice: document.getElementById("unit-price"),
    discount: document.getElementById("discount"),
    iva: document.getElementById("iva"),
    description: document.getElementById("description"),
    lineTotal: document.getElementById("line-total"),

    linesTableBody: document.getElementById("lines-table-body"),
    chargesTableBody: document.getElementById("charges-table-body"),

    clearLineBtn: document.getElementById("clear-line-btn"),
    addChargeBtn: document.getElementById("add-charge-btn"),
    calculatePaymentBtn: document.getElementById("calculate-payment-btn"),
    saveQuoteBtn: document.getElementById("save-quote-btn"),
    generatePdfBtn: document.getElementById("generate-pdf-btn"),

    otherChargeType: document.getElementById("other-charge-type"),
    otherChargeDetail: document.getElementById("other-charge-detail"),
    otherChargeAmount: document.getElementById("other-charge-amount"),
    paymentAmount: document.getElementById("payment-amount"),

    totalSale: document.getElementById("total-sale"),
    totalDiscount: document.getElementById("total-discount"),
    totalTax: document.getElementById("total-tax"),
    totalOtherCharges: document.getElementById("total-other-charges"),
    totalDocument: document.getElementById("total-document"),
    invoiceBalance: document.getElementById("invoice-balance"),
    newBalance: document.getElementById("new-balance")
};

initializeQuotation();
setupEvents();

function initializeQuotation() {
    if (!elements.quoteDate.value) {
        elements.quoteDate.value = getTodayISODate();
    }

    if (!elements.quoteNumber.value) {
        elements.quoteNumber.value = generateQuoteNumber();
    }

    updateLinePreview();
    renderLines();
    renderCharges();
    updateTotals();
}

function setupEvents() {
    elements.quoteClient.addEventListener("input", () => {
        elements.quoteClient.value = cleanClientName(elements.quoteClient.value);
    });

    if (elements.quotePhone) {
        elements.quotePhone.addEventListener("input", () => {
            elements.quotePhone.value = cleanPhoneNumber(elements.quotePhone.value);
        });
    }

    document.querySelectorAll(".type-btn").forEach((button) => {
        button.addEventListener("click", () => {
            document.querySelectorAll(".type-btn").forEach((btn) => {
                btn.classList.remove("active");
            });

            button.classList.add("active");

            if (elements.documentType) {
                elements.documentType.value = button.dataset.documentType;
            }
        });
    });

    elements.cabysCode.addEventListener("input", () => {
        elements.cabysCode.value = cleanCabysCode(elements.cabysCode.value);
    });

    elements.quantity.addEventListener("input", updateLinePreview);
    elements.unitPrice.addEventListener("input", updateLinePreview);
    elements.discount.addEventListener("input", updateLinePreview);
    elements.iva.addEventListener("change", updateLinePreview);

    elements.currency.addEventListener("change", () => {
        updateLinePreview();
        renderLines();
        renderCharges();
        updateTotals();
    });

    addLineBtn.addEventListener("click", addLine);
    elements.clearLineBtn.addEventListener("click", clearLineForm);
    elements.addChargeBtn.addEventListener("click", addCharge);
    elements.calculatePaymentBtn.addEventListener("click", calculateNewBalance);
    elements.saveQuoteBtn.addEventListener("click", saveQuote);
    elements.generatePdfBtn.addEventListener("click", generateQuotationPDF);
}

function getTodayISODate() {
    return new Date().toISOString().split("T")[0];
}

function generateQuoteNumber() {
    const currentCounter = Number(localStorage.getItem("revestikQuoteCounter")) || 1;
    return `COT-${String(currentCounter).padStart(4, "0")}`;
}

function increaseQuoteCounter() {
    const currentCounter = Number(localStorage.getItem("revestikQuoteCounter")) || 1;
    localStorage.setItem("revestikQuoteCounter", currentCounter + 1);
}

function cleanClientName(value) {
    return value.replace(/[^A-Za-zÁÉÍÓÚáéíóúÑñÜü\s.]/g, "");
}

function cleanPhoneNumber(value) {
    const numbers = value.replace(/\D/g, "").slice(0, 8);

    if (numbers.length > 4) {
        return `${numbers.slice(0, 4)}-${numbers.slice(4)}`;
    }

    return numbers;
}

function cleanCabysCode(value) {
    return value.replace(/\D/g, "").slice(0, 13);
}

function validateClientName() {
    const clientName = elements.quoteClient.value.trim();

    if (!clientName) {
        return true;
    }

    const regex = /^[A-Za-zÁÉÍÓÚáéíóúÑñÜü\s.]+$/;

    if (!regex.test(clientName)) {
        alert("El nombre del cliente solo puede contener letras, espacios y puntos.");
        elements.quoteClient.focus();
        return false;
    }

    return true;
}

function validatePhoneNumber() {
    if (!elements.quotePhone) {
        return true;
    }

    const phone = elements.quotePhone.value.trim();

    if (!phone) {
        return true;
    }

    if (!/^\d{4}-\d{4}$/.test(phone)) {
        alert("El teléfono debe tener el formato 8888-8888.");
        elements.quotePhone.focus();
        return false;
    }

    return true;
}

function validateCabysCode() {
    const cabysCode = elements.cabysCode.value.trim();

    if (!/^\d{13}$/.test(cabysCode)) {
        alert("El código CABYS debe contener exactamente 13 números.");
        elements.cabysCode.focus();
        return false;
    }

    return true;
}

function addLine() {
    const line = getCurrentLine();

    if (!line.description) {
        alert("Debes agregar una descripción.");
        elements.description.focus();
        return;
    }

    if (!validateCabysCode()) {
        return;
    }

    if (line.quantity <= 0) {
        alert("La cantidad debe ser mayor a 0.");
        elements.quantity.focus();
        return;
    }

    if (line.unitPrice <= 0) {
        alert("El precio unitario debe ser mayor a 0.");
        elements.unitPrice.focus();
        return;
    }

    state.lines.push(line);

    renderLines();
    updateTotals();
    clearLineForm();
}

function getCurrentLine() {
    const quantity = getNumber(elements.quantity.value);
    const unitPrice = getNumber(elements.unitPrice.value);
    const discount = getNumber(elements.discount.value);
    const ivaRate = getNumber(elements.iva.value);

    const grossSubtotal = quantity * unitPrice;
    const validDiscount = Math.min(discount, grossSubtotal);
    const taxableBase = grossSubtotal - validDiscount;
    const tax = taxableBase * ivaRate;
    const total = taxableBase + tax;

    return {
        productCode: elements.productCode.value.trim(),
        cabysCode: elements.cabysCode.value.trim(),
        unit: elements.unit.value,
        description: elements.description.value.trim(),
        quantity,
        unitPrice,
        grossSubtotal,
        discount: validDiscount,
        ivaRate,
        tax,
        total
    };
}

function updateLinePreview() {
    const line = getCurrentLine();
    elements.lineTotal.value = formatMoney(line.total);
}

function clearLineForm() {
    elements.productCode.value = "";
    elements.cabysCode.value = "";
    elements.unit.value = "Unidad";
    elements.quantity.value = 0;
    elements.unitPrice.value = 0;
    elements.discount.value = 0;
    elements.iva.value = "0.13";
    elements.description.value = "";
    elements.lineTotal.value = formatMoney(0);
}

function renderLines() {
    elements.linesTableBody.innerHTML = "";

    if (state.lines.length === 0) {
        elements.linesTableBody.innerHTML = `
            <tr class="empty-row">
                <td colspan="10">Sin líneas de detalle</td>
            </tr>
        `;
        return;
    }

    state.lines.forEach((line, index) => {
        const row = document.createElement("tr");

        row.innerHTML = `
            <td>${index + 1}</td>
            <td>${escapeHTML(line.productCode || "-")}</td>
            <td>${escapeHTML(line.description)}</td>
            <td>${escapeHTML(line.unit)}</td>
            <td>${line.quantity}</td>
            <td>${formatMoney(line.unitPrice)}</td>
            <td>${formatMoney(line.discount)}</td>
            <td>${formatMoney(line.tax)}</td>
            <td>${formatMoney(line.total)}</td>
            <td>
                <button class="delete-btn" type="button" data-line-index="${index}">
                    Borrar
                </button>
            </td>
        `;

        elements.linesTableBody.appendChild(row);
    });

    document.querySelectorAll("[data-line-index]").forEach((button) => {
        button.addEventListener("click", () => {
            const index = Number(button.dataset.lineIndex);
            state.lines.splice(index, 1);
            renderLines();
            updateTotals();
        });
    });
}

function addCharge() {
    const type = elements.otherChargeType.value;
    const detail = elements.otherChargeDetail.value.trim();
    const amount = getNumber(elements.otherChargeAmount.value);

    if (!detail) {
        alert("Debes agregar un detalle para el cargo.");
        elements.otherChargeDetail.focus();
        return;
    }

    if (amount <= 0) {
        alert("El monto del cargo debe ser mayor a 0.");
        elements.otherChargeAmount.focus();
        return;
    }

    state.charges.push({
        type,
        detail,
        amount
    });

    elements.otherChargeDetail.value = "";
    elements.otherChargeAmount.value = 0;

    renderCharges();
    updateTotals();
}

function renderCharges() {
    elements.chargesTableBody.innerHTML = "";

    if (state.charges.length === 0) {
        elements.chargesTableBody.innerHTML = `
            <tr class="empty-row">
                <td colspan="5">No hay otros cargos agregados</td>
            </tr>
        `;
        return;
    }

    state.charges.forEach((charge, index) => {
        const row = document.createElement("tr");

        row.innerHTML = `
            <td>${index + 1}</td>
            <td>${escapeHTML(charge.type)}</td>
            <td>${escapeHTML(charge.detail)}</td>
            <td>${formatMoney(charge.amount)}</td>
            <td>
                <button class="delete-btn" type="button" data-charge-index="${index}">
                    Borrar
                </button>
            </td>
        `;

        elements.chargesTableBody.appendChild(row);
    });

    document.querySelectorAll("[data-charge-index]").forEach((button) => {
        button.addEventListener("click", () => {
            const index = Number(button.dataset.chargeIndex);
            state.charges.splice(index, 1);
            renderCharges();
            updateTotals();
        });
    });
}

function getTotals() {
    const totalSale = state.lines.reduce((sum, line) => sum + line.grossSubtotal, 0);
    const totalDiscount = state.lines.reduce((sum, line) => sum + line.discount, 0);
    const totalTax = state.lines.reduce((sum, line) => sum + line.tax, 0);
    const totalOtherCharges = state.charges.reduce((sum, charge) => sum + charge.amount, 0);
    const documentTotal = totalSale - totalDiscount + totalTax + totalOtherCharges;

    return {
        totalSale,
        totalDiscount,
        totalTax,
        totalOtherCharges,
        documentTotal
    };
}

function updateTotals() {
    const totals = getTotals();

    elements.totalSale.textContent = formatMoney(totals.totalSale);
    elements.totalDiscount.textContent = formatMoney(totals.totalDiscount);
    elements.totalTax.textContent = formatMoney(totals.totalTax);
    elements.totalOtherCharges.textContent = formatMoney(totals.totalOtherCharges);
    elements.totalDocument.textContent = formatMoney(totals.documentTotal);

    elements.invoiceBalance.value = formatMoney(totals.documentTotal);
    elements.newBalance.value = formatMoney(totals.documentTotal);
}

function calculateNewBalance() {
    const totals = getTotals();
    const paymentAmount = getNumber(elements.paymentAmount.value);

    if (paymentAmount < 0) {
        alert("El pago no puede ser negativo.");
        elements.paymentAmount.focus();
        return;
    }

    if (paymentAmount > totals.documentTotal) {
        alert("El pago no puede ser mayor al total del documento.");
        elements.paymentAmount.focus();
        return;
    }

    elements.newBalance.value = formatMoney(totals.documentTotal - paymentAmount);
}

function saveQuote() {
    if (!validateClientName() || !validatePhoneNumber()) {
        return;
    }

    if (state.lines.length === 0) {
        alert("Debes agregar al menos una línea de detalle.");
        return;
    }

    const quote = buildQuoteObject();
    const savedQuotes = getSavedQuotes();

    savedQuotes.push(quote);

    localStorage.setItem("revestikQuotes", JSON.stringify(savedQuotes));
    localStorage.setItem("revestikLastQuote", JSON.stringify(quote));

    if (quote.documentType === "Venta") {
        const savedSales = getSavedSales();
        savedSales.push(quote);
        localStorage.setItem("revestikSales", JSON.stringify(savedSales));
    }

    increaseQuoteCounter();

    alert(`${quote.documentType} guardada correctamente. La próxima cotización será ${generateQuoteNumber()}.`);
}

function buildQuoteObject() {
    const clientName = elements.quoteClient.value.trim();
    const clientPhone = elements.quotePhone ? elements.quotePhone.value.trim() : "";
    const documentType = elements.documentType ? elements.documentType.value : "Cotización";

    return {
        quoteNumber: elements.quoteNumber.value.trim() || generateQuoteNumber(),
        documentType,
        client: clientName || "Cliente no especificado",
        phone: clientPhone || "No especificado",
        date: elements.quoteDate.value,
        validity: elements.quoteValidity.value.trim(),
        currency: elements.currency.value,
        lines: state.lines,
        charges: state.charges,
        totals: getTotals(),
        createdAt: new Date().toISOString()
    };
}

function getSavedQuotes() {
    try {
        return JSON.parse(localStorage.getItem("revestikQuotes")) || [];
    } catch (error) {
        return [];
    }
}

function getSavedSales() {
    try {
        return JSON.parse(localStorage.getItem("revestikSales")) || [];
    } catch (error) {
        return [];
    }
}

async function generateQuotationPDF() {
    if (!validateClientName() || !validatePhoneNumber()) {
        return;
    }

    if (state.lines.length === 0) {
        alert("Debes agregar al menos una línea de detalle antes de generar el PDF.");
        return;
    }

    if (!window.jspdf || !window.jspdf.jsPDF) {
        alert("No se cargó jsPDF. Revisa los scripts en cotizaciones.html.");
        return;
    }

    const { jsPDF } = window.jspdf;
    const doc = new jsPDF("p", "mm", "a4");

    if (typeof doc.autoTable !== "function") {
        alert("No se cargó jsPDF AutoTable. Revisa los scripts en cotizaciones.html.");
        return;
    }

    const quote = buildQuoteObject();
    const totals = quote.totals;
    const pageWidth = doc.internal.pageSize.getWidth();

    try {
        const logoUrl = new URL("../assets/logo/revestik.svg", window.location.href).href;
        const logoData = await loadImageAsDataURL(logoUrl);
        doc.addImage(logoData, "PNG", 14, 10, 48, 20);
    } catch (error) {
        doc.setFont("helvetica", "bold");
        doc.setFontSize(22);
        doc.setTextColor(31, 41, 55);
        doc.text("Revestik", 14, 22);
    }

    doc.setFont("helvetica", "bold");
    doc.setFontSize(20);
    doc.setTextColor(245, 158, 11);
    doc.text(quote.documentType, pageWidth - 14, 18, { align: "right" });

    doc.setFont("helvetica", "normal");
    doc.setFontSize(10);
    doc.setTextColor(31, 41, 55);
    doc.text(`N.º: ${quote.quoteNumber}`, pageWidth - 14, 26, { align: "right" });
    doc.text(`Fecha: ${formatDate(quote.date)}`, pageWidth - 14, 32, { align: "right" });
    doc.text(`Moneda: ${quote.currency}`, pageWidth - 14, 38, { align: "right" });

    doc.setDrawColor(245, 158, 11);
    doc.setLineWidth(0.8);
    doc.line(14, 45, pageWidth - 14, 45);

    let currentY = 55;

    doc.setFont("helvetica", "bold");
    doc.setFontSize(12);
    doc.setTextColor(31, 41, 55);
    doc.text("Cliente", 14, currentY);

    doc.setFont("helvetica", "normal");
    doc.setFontSize(10);
    doc.text(`Nombre: ${quote.client}`, 14, currentY + 7);
    doc.text(`Teléfono: ${quote.phone}`, 14, currentY + 13);

    doc.setFont("helvetica", "bold");
    doc.setFontSize(12);
    doc.text("Condiciones", 115, currentY);

    doc.setFont("helvetica", "normal");
    doc.setFontSize(10);
    doc.text(`Validez: ${quote.validity || "No especificada"}`, 115, currentY + 7);
    doc.text(`Tipo: ${quote.documentType}`, 115, currentY + 13);

    currentY += 25;

    doc.setFont("helvetica", "bold");
    doc.setFontSize(13);
    doc.text("Líneas de detalle", 14, currentY);

    const lineRows = quote.lines.map((line, index) => [
        index + 1,
        line.productCode || "-",
        line.description,
        line.unit,
        line.quantity,
        formatMoneyPDF(line.unitPrice),
        formatMoneyPDF(line.discount),
        formatMoneyPDF(line.tax),
        formatMoneyPDF(line.total)
    ]);

    doc.autoTable({
        startY: currentY + 6,
        head: [["#", "Cód.", "Descripción", "Unid.", "Cant.", "Precio", "Desc.", "IVA", "Total"]],
        body: lineRows,
        theme: "grid",
        styles: {
            font: "helvetica",
            fontSize: 8,
            cellPadding: 2,
            valign: "top"
        },
        headStyles: {
            fillColor: [31, 41, 55],
            textColor: [255, 255, 255],
            fontStyle: "bold"
        },
        columnStyles: {
            0: { cellWidth: 8 },
            1: { cellWidth: 18 },
            2: { cellWidth: 48 },
            3: { cellWidth: 16 },
            4: { cellWidth: 14 },
            5: { cellWidth: 22 },
            6: { cellWidth: 20 },
            7: { cellWidth: 20 },
            8: { cellWidth: 24 }
        },
        margin: {
            left: 14,
            right: 14
        }
    });

    currentY = doc.lastAutoTable.finalY + 12;

    doc.setFont("helvetica", "bold");
    doc.setFontSize(13);
    doc.text("Otros cargos", 14, currentY);

    const chargeRows = quote.charges.length > 0
        ? quote.charges.map((charge, index) => [
            index + 1,
            charge.type,
            charge.detail,
            formatMoneyPDF(charge.amount)
        ])
        : [["-", "-", "No hay otros cargos", formatMoneyPDF(0)]];

    doc.autoTable({
        startY: currentY + 6,
        head: [["#", "Tipo", "Detalle", "Monto"]],
        body: chargeRows,
        theme: "grid",
        styles: {
            font: "helvetica",
            fontSize: 9,
            cellPadding: 2
        },
        headStyles: {
            fillColor: [31, 41, 55],
            textColor: [255, 255, 255],
            fontStyle: "bold"
        },
        margin: {
            left: 14,
            right: 14
        }
    });

    currentY = doc.lastAutoTable.finalY + 12;

    if (currentY > 220) {
        doc.addPage();
        currentY = 20;
    }

    drawTotals(doc, totals, currentY);

    currentY += 55;

    if (currentY > 265) {
        doc.addPage();
        currentY = 20;
    }

    doc.setDrawColor(229, 231, 235);
    doc.line(14, currentY, pageWidth - 14, currentY);

    currentY += 8;

    doc.setFont("helvetica", "bold");
    doc.setFontSize(10);
    doc.setTextColor(31, 41, 55);
    doc.text("Notas:", 14, currentY);

    doc.setFont("helvetica", "normal");
    doc.setFontSize(9);
    doc.setTextColor(107, 114, 128);
    doc.text("Esta cotización está sujeta a disponibilidad de inventario y condiciones comerciales vigentes.", 14, currentY + 6);
    doc.text("Documento generado desde Revestik.", 14, currentY + 12);

    const fileName = `${quote.quoteNumber}-${quote.client}.pdf`
        .replace(/\s+/g, "-")
        .replace(/[^\wÁÉÍÓÚáéíóúÑñ.-]/g, "");

    doc.save(fileName);
}

function drawTotals(doc, totals, currentY) {
    const totalsX = 115;
    const totalsWidth = 80;
    const rowHeight = 8;

    const totalsRows = [
        ["Total venta", formatMoneyPDF(totals.totalSale)],
        ["Total descuento", formatMoneyPDF(totals.totalDiscount)],
        ["Total impuesto", formatMoneyPDF(totals.totalTax)],
        ["Otros cargos", formatMoneyPDF(totals.totalOtherCharges)]
    ];

    totalsRows.forEach((row, index) => {
        const y = currentY + index * rowHeight;

        doc.setDrawColor(229, 231, 235);
        doc.setFillColor(249, 250, 251);
        doc.rect(totalsX, y, totalsWidth, rowHeight, "FD");

        doc.setFont("helvetica", "normal");
        doc.setFontSize(10);
        doc.setTextColor(31, 41, 55);
        doc.text(row[0], totalsX + 3, y + 5.5);

        doc.setFont("helvetica", "bold");
        doc.text(row[1], totalsX + totalsWidth - 3, y + 5.5, { align: "right" });
    });

    const finalY = currentY + totalsRows.length * rowHeight;

    doc.setDrawColor(229, 231, 235);
    doc.setFillColor(255, 247, 237);
    doc.rect(totalsX, finalY, totalsWidth, rowHeight + 2, "FD");

    doc.setFont("helvetica", "bold");
    doc.setFontSize(11);
    doc.setTextColor(31, 41, 55);
    doc.text(`Total ${elements.documentType ? elements.documentType.value.toLowerCase() : "cotización"}`, totalsX + 3, finalY + 6.5);
    doc.text(formatMoneyPDF(totals.documentTotal), totalsX + totalsWidth - 3, finalY + 6.5, { align: "right" });
}

function getNumber(value) {
    return Number(value) || 0;
}

function formatMoney(amount) {
    const currency = elements.currency.value;
    const symbol = currency === "CRC" ? "₡" : "$";

    return `${symbol}${amount.toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    })}`;
}

function formatMoneyPDF(amount) {
    const currency = elements.currency.value;
    const symbol = currency === "CRC" ? "CRC " : "$";

    return `${symbol}${amount.toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    })}`;
}

function formatDate(dateValue) {
    if (!dateValue) {
        return "No especificada";
    }

    const date = new Date(dateValue + "T00:00:00");

    return date.toLocaleDateString("es-CR", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit"
    });
}

function escapeHTML(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

function loadImageAsDataURL(imageUrl) {
    return new Promise((resolve, reject) => {
        const image = new Image();

        image.onload = () => {
            const canvas = document.createElement("canvas");

            canvas.width = image.width;
            canvas.height = image.height;

            const context = canvas.getContext("2d");

            context.fillStyle = "#ffffff";
            context.fillRect(0, 0, canvas.width, canvas.height);
            context.drawImage(image, 0, 0);

            resolve(canvas.toDataURL("image/png"));
        };

        image.onerror = () => {
            reject(new Error("No se pudo cargar el logo."));
        };

        image.src = imageUrl;
    });
}

}

function setupInventoryModule() {
const form = document.getElementById("inventory-form");


if (!form) {
    return;
}

const INVENTORY_KEY = "revestikInventory";

const state = {
    products: [],
    editingProductId: null
};

const elements = {
    form,
    code: document.getElementById("inventory-code"),
    name: document.getElementById("inventory-name"),
    category: document.getElementById("inventory-category"),
    supplier: document.getElementById("inventory-supplier"),
    cabys: document.getElementById("inventory-cabys"),
    unit: document.getElementById("inventory-unit"),
    cost: document.getElementById("inventory-cost"),
    price: document.getElementById("inventory-price"),
    stock: document.getElementById("inventory-stock"),
    minimumStock: document.getElementById("inventory-min-stock"),
    status: document.getElementById("inventory-status"),

    clearBtn: document.getElementById("clear-inventory-btn"),
    saveBtn: document.getElementById("save-inventory-btn"),
    search: document.getElementById("inventory-search"),
    tableBody: document.getElementById("inventory-table-body"),

    totalProducts: document.getElementById("inventory-total-products"),
    totalStock: document.getElementById("inventory-total-stock"),
    lowStock: document.getElementById("inventory-low-stock"),
    totalValue: document.getElementById("inventory-total-value")
};

initializeInventory();
setupInventoryEvents();

function initializeInventory() {
    state.products = getStoredProducts();
    renderInventory();
    updateInventorySummary();
}

function setupInventoryEvents() {
    elements.form.addEventListener("submit", saveProduct);
    elements.clearBtn.addEventListener("click", clearInventoryForm);
    elements.search.addEventListener("input", renderInventory);

    elements.code.addEventListener("input", () => {
        elements.code.value = cleanProductCode(elements.code.value);
    });

    elements.cabys.addEventListener("input", () => {
        elements.cabys.value = cleanCabysCode(elements.cabys.value);
    });

    elements.tableBody.addEventListener("click", handleTableActions);
}

function saveProduct(event) {
    event.preventDefault();

    if (!validateProductForm()) {
        return;
    }

    const product = buildProductObject();

    if (state.editingProductId) {
        state.products = state.products.map((item) => {
            if (item.id === state.editingProductId) {
                return product;
            }

            return item;
        });

        alert("Producto actualizado correctamente.");
    } else {
        state.products.push(product);
        alert("Producto guardado correctamente.");
    }

    saveProductsToStorage();
    clearInventoryForm();
    renderInventory();
    updateInventorySummary();
}

function buildProductObject() {
    const existingProduct = state.products.find((product) => {
        return product.id === state.editingProductId;
    });

    return {
        id: state.editingProductId || createProductId(),
        code: elements.code.value.trim(),
        name: elements.name.value.trim(),
        category: elements.category.value,
        supplier: elements.supplier.value.trim(),
        cabysCode: elements.cabys.value.trim(),
        unit: elements.unit.value,
        cost: getNumber(elements.cost.value),
        salePrice: getNumber(elements.price.value),
        stock: getNumber(elements.stock.value),
        minimumStock: getNumber(elements.minimumStock.value),
        status: elements.status.value,
        createdAt: existingProduct ? existingProduct.createdAt : new Date().toISOString(),
        updatedAt: new Date().toISOString()
    };
}

function validateProductForm() {
    const code = elements.code.value.trim();
    const name = elements.name.value.trim();
    const cabysCode = elements.cabys.value.trim();
    const cost = getNumber(elements.cost.value);
    const salePrice = elements.salePrice ? getNumber(elements.salePrice.value) : 0;
    const stock = getNumber(elements.stock.value);
    const minimumStock = getNumber(elements.minimumStock.value);

    if (!code) {
        alert("Debes agregar un código de producto.");
        elements.code.focus();
        return false;
    }

    if (!name) {
        alert("Debes agregar el nombre o descripción del producto.");
        elements.name.focus();
        return false;
    }

    const duplicatedProduct = state.products.find((product) => {
        return product.code.toLowerCase() === code.toLowerCase() && product.id !== state.editingProductId;
    });

    if (duplicatedProduct) {
        alert("Ya existe un producto con ese código.");
        elements.code.focus();
        return false;
    }

    if (cabysCode && !/^\d{13}$/.test(cabysCode)) {
        alert("El código CABYS debe contener exactamente 13 números.");
        elements.cabys.focus();
        return false;
    }

    if (cost < 0) {
        alert("El costo no puede ser negativo.");
        elements.cost.focus();
        return false;
    }

    if (elements.salePrice && salePrice < 0) {
        alert("El precio de venta no puede ser negativo.");
        elements.salePrice.focus();
        return false;
    }

    if (stock < 0) {
        alert("El stock no puede ser negativo.");
        elements.stock.focus();
        return false;
    }

    if (minimumStock < 0) {
        alert("El stock mínimo no puede ser negativo.");
        elements.minimumStock.focus();
        return false;
    }

    return true;
}

function renderInventory() {
    const products = getFilteredProducts();

    elements.tableBody.innerHTML = "";

    if (products.length === 0) {
        elements.tableBody.innerHTML = `
            <tr class="empty-row">
                <td colspan="9">No hay productos registrados</td>
            </tr>
        `;
        return;
    }

    products.forEach((product) => {
        const row = document.createElement("tr");
        const stockClass = product.stock <= product.minimumStock ? "stock-low" : "stock-ok";
        const statusClass = product.status === "Activo" ? "status-active" : "status-inactive";

        row.innerHTML = `
            <td>${escapeHTML(product.code)}</td>
            <td>
                <strong>${escapeHTML(product.name)}</strong>
                <br>
                <small>${escapeHTML(product.supplier || "Sin proveedor")}</small>
            </td>
            <td>${escapeHTML(product.category)}</td>
            <td>${escapeHTML(product.unit)}</td>
            <td>
                <span class="stock-badge ${stockClass}">
                    ${product.stock}
                </span>
            </td>
            <td>${formatInventoryMoney(product.cost)}</td>
            <td>${formatInventoryMoney(product.salePrice)}</td>
            <td>
                <span class="status-badge ${statusClass}">
                    ${escapeHTML(product.status)}
                </span>
            </td>
            <td>
                <button class="secondary-btn inventory-action-btn" type="button" data-edit-id="${product.id}">
                    Editar
                </button>
                <button class="delete-btn inventory-action-btn" type="button" data-delete-id="${product.id}">
                    Borrar
                </button>
            </td>
        `;

        elements.tableBody.appendChild(row);
    });
}

function getFilteredProducts() {
    const searchTerm = elements.search.value.trim().toLowerCase();

    if (!searchTerm) {
        return state.products;
    }

    return state.products.filter((product) => {
        return product.code.toLowerCase().includes(searchTerm) ||
            product.name.toLowerCase().includes(searchTerm) ||
            product.category.toLowerCase().includes(searchTerm) ||
            product.supplier.toLowerCase().includes(searchTerm);
    });
}

function handleTableActions(event) {
    const editId = event.target.dataset.editId;
    const deleteId = event.target.dataset.deleteId;

    if (editId) {
        editProduct(editId);
    }

    if (deleteId) {
        deleteProduct(deleteId);
    }
}

function editProduct(productId) {
    const product = state.products.find((item) => {
        return item.id === productId;
    });

    if (!product) {
        return;
    }

    state.editingProductId = product.id;

    elements.code.value = product.code;
    elements.name.value = product.name;
    elements.category.value = product.category;
    elements.supplier.value = product.supplier;
    elements.cabys.value = product.cabysCode;
    elements.unit.value = product.unit;
    elements.cost.value = product.cost;
    elements.price.value = product.salePrice;
    elements.stock.value = product.stock;
    elements.minimumStock.value = product.minimumStock;
    elements.status.value = product.status;
    elements.saveBtn.textContent = "Actualizar producto";

    elements.form.scrollIntoView({
        behavior: "smooth",
        block: "start"
    });
}

function deleteProduct(productId) {
    const product = state.products.find((item) => {
        return item.id === productId;
    });

    if (!product) {
        return;
    }

    const confirmDelete = confirm(`¿Deseas borrar el producto ${product.code}?`);

    if (!confirmDelete) {
        return;
    }

    state.products = state.products.filter((item) => {
        return item.id !== productId;
    });

    if (state.editingProductId === productId) {
        clearInventoryForm();
    }

    saveProductsToStorage();
    renderInventory();
    updateInventorySummary();
}

function updateInventorySummary() {
    const activeProducts = state.products.filter((product) => {
        return product.status === "Activo";
    });

    const totalProducts = state.products.length;

    const totalStock = activeProducts.reduce((sum, product) => {
        return sum + product.stock;
    }, 0);

    const lowStock = activeProducts.filter((product) => {
        return product.stock <= product.minimumStock;
    }).length;

    const totalValue = activeProducts.reduce((sum, product) => {
        return sum + product.cost * product.stock;
    }, 0);

    elements.totalProducts.textContent = totalProducts;
    elements.totalStock.textContent = totalStock;
    elements.lowStock.textContent = lowStock;
    elements.totalValue.textContent = formatInventoryMoney(totalValue);
}

function clearInventoryForm() {
    state.editingProductId = null;

    elements.code.value = "";
    elements.name.value = "";
    elements.category.value = "Porcelanato";
    elements.supplier.value = "";
    elements.cabys.value = "";
    elements.unit.value = "Unidad";
    elements.cost.value = 0;
    elements.price.value = 0;
    elements.stock.value = 0;
    elements.minimumStock.value = 0;
    elements.status.value = "Activo";
    elements.saveBtn.textContent = "Guardar producto";
}

function getStoredProducts() {
    try {
        return JSON.parse(localStorage.getItem(INVENTORY_KEY)) || [];
    } catch (error) {
        return [];
    }
}

function saveProductsToStorage() {
    localStorage.setItem(INVENTORY_KEY, JSON.stringify(state.products));
}

function createProductId() {
    if (window.crypto && window.crypto.randomUUID) {
        return window.crypto.randomUUID();
    }

    return `product-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
}

function cleanProductCode(value) {
    return value.toUpperCase().replace(/[^A-Z0-9-]/g, "").slice(0, 25);
}

function cleanCabysCode(value) {
    return value.replace(/\D/g, "").slice(0, 13);
}

function getNumber(value) {
    return Number(value) || 0;
}

function formatInventoryMoney(amount) {
    return `₡${amount.toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    })}`;
}

function escapeHTML(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}


}

function setupPurchaseModule() {
const form = document.getElementById("purchase-form");


if (!form) {
    return;
}

const PURCHASES_KEY = "revestikPurchases";
const INVENTORY_KEY = "revestikInventory";
const PURCHASE_COUNTER_KEY = "revestikPurchaseCounter";

const state = {
    purchases: [],
    editingPurchaseId: null
};

const elements = {
    form,
    purchaseNumber: document.getElementById("purchase-number"),
    date: document.getElementById("purchase-date"),
    supplier: document.getElementById("purchase-supplier"),
    invoice: document.getElementById("purchase-invoice"),
    productCode: document.getElementById("purchase-product-code"),
    cabys: document.getElementById("purchase-cabys"),
    productName: document.getElementById("purchase-product-name"),
    category: document.getElementById("purchase-category"),
    unit: document.getElementById("purchase-unit"),
    quantity: document.getElementById("purchase-quantity"),
    unitCost: document.getElementById("purchase-unit-cost"),
    salePrice: document.getElementById("purchase-sale-price"),
    extraCost: document.getElementById("purchase-extra-cost"),
    total: document.getElementById("purchase-total"),
    paymentStatus: document.getElementById("purchase-payment-status"),
    inventoryAction: document.getElementById("purchase-inventory-action"),
    notes: document.getElementById("purchase-notes"),

    clearBtn: document.getElementById("clear-purchase-btn"),
    saveBtn: document.getElementById("save-purchase-btn"),
    search: document.getElementById("purchase-search"),
    tableBody: document.getElementById("purchase-table-body"),

    totalCount: document.getElementById("purchase-total-count"),
    totalAmount: document.getElementById("purchase-total-amount"),
    pendingAmount: document.getElementById("purchase-pending-amount"),
    totalUnits: document.getElementById("purchase-total-units")
};

initializePurchases();
setupPurchaseEvents();

function initializePurchases() {
    state.purchases = getStoredPurchases();

    if (!elements.purchaseNumber.value) {
        elements.purchaseNumber.value = generatePurchaseNumber();
    }

    if (!elements.date.value) {
        elements.date.value = getTodayISODate();
    }

    updatePurchaseTotal();
    renderPurchases();
    updatePurchaseSummary();
}

function setupPurchaseEvents() {
    elements.form.addEventListener("submit", savePurchase);
    elements.clearBtn.addEventListener("click", clearPurchaseForm);
    elements.search.addEventListener("input", renderPurchases);

    elements.productCode.addEventListener("input", () => {
        elements.productCode.value = cleanProductCode(elements.productCode.value);
    });

    if (elements.cabys) {
        elements.cabys.addEventListener("input", () => {
            elements.cabys.value = cleanCabysCode(elements.cabys.value);
        });
    }

    elements.quantity.addEventListener("input", updatePurchaseTotal);
    elements.unitCost.addEventListener("input", updatePurchaseTotal);
    elements.extraCost.addEventListener("input", updatePurchaseTotal);

    elements.tableBody.addEventListener("click", handlePurchaseActions);
}

function savePurchase(event) {
    event.preventDefault();

    if (!validatePurchaseForm()) {
        return;
    }

    const existingPurchase = getEditingPurchase();
    const purchase = buildPurchaseObject(existingPurchase);

    if (existingPurchase && existingPurchase.inventoryApplied) {
        reverseInventoryFromPurchase(existingPurchase);
    }

    if (purchase.inventoryAction === "Aplicar a inventario") {
        applyPurchaseToInventory(purchase);
        purchase.inventoryApplied = true;
    }

    if (state.editingPurchaseId) {
        state.purchases = state.purchases.map((item) => {
            if (item.id === state.editingPurchaseId) {
                return purchase;
            }

            return item;
        });

        alert("Compra actualizada correctamente.");
    } else {
        state.purchases.push(purchase);
        increasePurchaseCounter();
        alert("Compra guardada correctamente.");
    }

    savePurchasesToStorage();
    clearPurchaseForm();
    renderPurchases();
    updatePurchaseSummary();
}

function buildPurchaseObject(existingPurchase) {
    const quantity = getNumber(elements.quantity.value);
    const unitCost = getNumber(elements.unitCost.value);
    const salePrice = elements.salePrice ? getNumber(elements.salePrice.value) : 0;
    const extraCost = getNumber(elements.extraCost.value);
    const total = quantity * unitCost + extraCost;

    return {
        id: state.editingPurchaseId || createId("purchase"),
        purchaseNumber: elements.purchaseNumber.value.trim() || generatePurchaseNumber(),
        date: elements.date.value,
        supplier: elements.supplier.value.trim(),
        invoice: elements.invoice.value.trim(),
        productCode: elements.productCode.value.trim(),
        cabysCode: elements.cabys ? elements.cabys.value.trim() : "",
        productName: elements.productName.value.trim(),
        category: elements.category.value,
        unit: elements.unit.value,
        quantity,
        unitCost,
        extraCost,
        salePrice,
        total,
        paymentStatus: elements.paymentStatus.value,
        inventoryAction: elements.inventoryAction.value,
        inventoryApplied: false,
        notes: elements.notes.value.trim(),
        createdAt: existingPurchase ? existingPurchase.createdAt : new Date().toISOString(),
        updatedAt: new Date().toISOString()
    };
}

function validatePurchaseForm() {
    const supplier = elements.supplier.value.trim();
    const productCode = elements.productCode.value.trim();
    const productName = elements.productName.value.trim();
    const cabysCode = elements.cabys ? elements.cabys.value.trim() : "";
    const quantity = getNumber(elements.quantity.value);
    const unitCost = getNumber(elements.unitCost.value);
    const salePrice = getNumber(elements.salePrice.value);
    const extraCost = getNumber(elements.extraCost.value);

    if (!elements.date.value) {
        alert("Debes agregar la fecha de la compra.");
        elements.date.focus();
        return false;
    }

    if (!supplier) {
        alert("Debes agregar el proveedor.");
        elements.supplier.focus();
        return false;
    }

    if (!productCode) {
        alert("Debes agregar el código del producto.");
        elements.productCode.focus();
        return false;
    }

    if (!productName) {
        alert("Debes agregar el producto o descripción.");
        elements.productName.focus();
        return false;
    }

    if (cabysCode && !/^\d{13}$/.test(cabysCode)) {
        alert("El código CABYS debe contener exactamente 13 números.");
        elements.cabys.focus();
        return false;
    }

    if (quantity <= 0) {
        alert("La cantidad debe ser mayor a 0.");
        elements.quantity.focus();
        return false;
    }

    if (unitCost < 0) {
        alert("El costo unitario no puede ser negativo.");
        elements.unitCost.focus();
        return false;
    }

    if (extraCost < 0) {
        alert("Los costos adicionales no pueden ser negativos.");
        elements.extraCost.focus();
        return false;
    }

    return true;
}

function updatePurchaseTotal() {
    const quantity = getNumber(elements.quantity.value);
    const unitCost = getNumber(elements.unitCost.value);
    const extraCost = getNumber(elements.extraCost.value);
    const total = quantity * unitCost + extraCost;

    elements.total.value = total > 0 ? formatPlainMoney(total) : "";
}

function renderPurchases() {
    const purchases = getFilteredPurchases();

    elements.tableBody.innerHTML = "";

    if (purchases.length === 0) {
        elements.tableBody.innerHTML = `
            <tr class="empty-row">
                <td colspan="10">No hay compras registradas</td>
            </tr>
        `;
        return;
    }

    purchases.forEach((purchase) => {
        const row = document.createElement("tr");

        row.innerHTML = `
            <td>${formatDate(purchase.date)}</td>
            <td>${escapeHTML(purchase.purchaseNumber)}</td>
            <td>${escapeHTML(purchase.supplier)}</td>
            <td>${escapeHTML(purchase.invoice || "-")}</td>
            <td>
                <strong>${escapeHTML(purchase.productCode)}</strong>
                <br>
                <small>${escapeHTML(purchase.productName)}</small>
            </td>
            <td>${purchase.quantity} ${escapeHTML(purchase.unit)}</td>
            <td>${formatPlainMoney(purchase.total)}</td>
            <td>
                <span class="purchase-status ${getPaymentStatusClass(purchase.paymentStatus)}">
                    ${escapeHTML(purchase.paymentStatus)}
                </span>
            </td>
            <td>
                <span class="purchase-status ${purchase.inventoryApplied ? "inventory-applied" : "inventory-not-applied"}">
                    ${purchase.inventoryApplied ? "Aplicado" : "No aplicado"}
                </span>
            </td>
            <td>
                <button class="secondary-btn purchase-action-btn" type="button" data-edit-purchase-id="${purchase.id}">
                    Editar
                </button>
                <button class="delete-btn purchase-action-btn" type="button" data-delete-purchase-id="${purchase.id}">
                    Borrar
                </button>
            </td>
        `;

        elements.tableBody.appendChild(row);
    });
}

function getFilteredPurchases() {
    const searchTerm = elements.search.value.trim().toLowerCase();

    if (!searchTerm) {
        return state.purchases;
    }

    return state.purchases.filter((purchase) => {
        return purchase.purchaseNumber.toLowerCase().includes(searchTerm) ||
            purchase.supplier.toLowerCase().includes(searchTerm) ||
            purchase.invoice.toLowerCase().includes(searchTerm) ||
            purchase.productCode.toLowerCase().includes(searchTerm) ||
            purchase.productName.toLowerCase().includes(searchTerm);
    });
}

function handlePurchaseActions(event) {
    const editId = event.target.dataset.editPurchaseId;
    const deleteId = event.target.dataset.deletePurchaseId;

    if (editId) {
        editPurchase(editId);
    }

    if (deleteId) {
        deletePurchase(deleteId);
    }
}

function editPurchase(purchaseId) {
    const purchase = state.purchases.find((item) => {
        return item.id === purchaseId;
    });

    if (!purchase) {
        return;
    }

    state.editingPurchaseId = purchase.id;

    elements.purchaseNumber.value = purchase.purchaseNumber;
    elements.date.value = purchase.date;
    elements.supplier.value = purchase.supplier;
    elements.invoice.value = purchase.invoice;
    elements.productCode.value = purchase.productCode;

    if (elements.cabys) {
        elements.cabys.value = purchase.cabysCode || "";
    }

    elements.productName.value = purchase.productName;
    elements.category.value = purchase.category;
    elements.unit.value = purchase.unit;
    elements.quantity.value = purchase.quantity;
    elements.unitCost.value = purchase.unitCost;
    if (elements.salePrice) {
    elements.salePrice.value = purchase.salePrice || "";
    }
    elements.extraCost.value = purchase.extraCost;
    elements.paymentStatus.value = purchase.paymentStatus;
    elements.inventoryAction.value = purchase.inventoryApplied ? "Aplicar a inventario" : purchase.inventoryAction;
    elements.notes.value = purchase.notes;
    elements.saveBtn.textContent = "Actualizar compra";

    updatePurchaseTotal();

    elements.form.scrollIntoView({
        behavior: "smooth",
        block: "start"
    });
}

function deletePurchase(purchaseId) {
    const purchase = state.purchases.find((item) => {
        return item.id === purchaseId;
    });

    if (!purchase) {
        return;
    }

    const confirmDelete = confirm(`¿Deseas borrar la compra ${purchase.purchaseNumber}?`);

    if (!confirmDelete) {
        return;
    }

    if (purchase.inventoryApplied) {
        reverseInventoryFromPurchase(purchase);
    }

    state.purchases = state.purchases.filter((item) => {
        return item.id !== purchaseId;
    });

    if (state.editingPurchaseId === purchaseId) {
        clearPurchaseForm();
    }

    savePurchasesToStorage();
    renderPurchases();
    updatePurchaseSummary();
}

function applyPurchaseToInventory(purchase) {
    const inventory = getStoredInventory();

    const existingProduct = inventory.find((product) => {
        return product.code.toLowerCase() === purchase.productCode.toLowerCase();
    });

    if (existingProduct) {
        existingProduct.name = purchase.productName;
        existingProduct.category = purchase.category;
        existingProduct.supplier = purchase.supplier;
        existingProduct.cabysCode = purchase.cabysCode;
        existingProduct.unit = purchase.unit;
        existingProduct.cost = purchase.unitCost;

        if (purchase.salePrice > 0) {
            existingProduct.salePrice = purchase.salePrice;
        }

        existingProduct.stock = getNumber(existingProduct.stock) + purchase.quantity;
        existingProduct.status = "Activo";
        existingProduct.updatedAt = new Date().toISOString();
    } else {
        inventory.push({
            id: createId("product"),
            code: purchase.productCode,
            name: purchase.productName,
            category: purchase.category,
            supplier: purchase.supplier,
            cabysCode: purchase.cabysCode,
            unit: purchase.unit,
            cost: purchase.unitCost,
            salePrice: purchase.salePrice,
            stock: purchase.quantity,
            minimumStock: 0,
            status: "Activo",
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString()
        });
    }

    saveInventoryToStorage(inventory);
}

function reverseInventoryFromPurchase(purchase) {
    const inventory = getStoredInventory();
    const existingProduct = inventory.find((product) => {
        return product.code.toLowerCase() === purchase.productCode.toLowerCase();
    });

    if (!existingProduct) {
        return;
    }

    existingProduct.stock = Math.max(0, getNumber(existingProduct.stock) - purchase.quantity);
    existingProduct.updatedAt = new Date().toISOString();

    saveInventoryToStorage(inventory);
}

function updatePurchaseSummary() {
    const totalCount = state.purchases.length;

    const totalAmount = state.purchases.reduce((sum, purchase) => {
        return sum + purchase.total;
    }, 0);

    const pendingAmount = state.purchases.reduce((sum, purchase) => {
        if (purchase.paymentStatus === "Pendiente" || purchase.paymentStatus === "Abonado") {
            return sum + purchase.total;
        }

        return sum;
    }, 0);

    const totalUnits = state.purchases.reduce((sum, purchase) => {
        return sum + purchase.quantity;
    }, 0);

    elements.totalCount.textContent = totalCount || "—";
    elements.totalAmount.textContent = totalAmount > 0 ? formatPlainMoney(totalAmount) : "—";
    elements.pendingAmount.textContent = pendingAmount > 0 ? formatPlainMoney(pendingAmount) : "—";
    elements.totalUnits.textContent = totalUnits || "—";
}

function clearPurchaseForm() {
    state.editingPurchaseId = null;

    elements.purchaseNumber.value = generatePurchaseNumber();
    elements.date.value = getTodayISODate();
    elements.supplier.value = "";
    elements.invoice.value = "";
    elements.productCode.value = "";

    if (elements.cabys) {
        elements.cabys.value = "";
    }

    elements.productName.value = "";
    elements.category.value = "Porcelanato";
    elements.unit.value = "Unidad";
    elements.quantity.value = "";
    elements.unitCost.value = "";
    if (elements.salePrice) {
    elements.salePrice.value = "";
    }
    elements.extraCost.value = "";
    elements.total.value = "";
    elements.paymentStatus.value = "Pendiente";
    elements.inventoryAction.value = "No aplicado";
    elements.notes.value = "";
    elements.saveBtn.textContent = "Guardar compra";
}

function getEditingPurchase() {
    if (!state.editingPurchaseId) {
        return null;
    }

    return state.purchases.find((purchase) => {
        return purchase.id === state.editingPurchaseId;
    }) || null;
}

function getStoredPurchases() {
    try {
        return JSON.parse(localStorage.getItem(PURCHASES_KEY)) || [];
    } catch (error) {
        return [];
    }
}

function savePurchasesToStorage() {
    localStorage.setItem(PURCHASES_KEY, JSON.stringify(state.purchases));
}

function getStoredInventory() {
    try {
        return JSON.parse(localStorage.getItem(INVENTORY_KEY)) || [];
    } catch (error) {
        return [];
    }
}

function saveInventoryToStorage(inventory) {
    localStorage.setItem(INVENTORY_KEY, JSON.stringify(inventory));
}

function generatePurchaseNumber() {
    const currentCounter = Number(localStorage.getItem(PURCHASE_COUNTER_KEY)) || 1;
    return `COMP-${String(currentCounter).padStart(4, "0")}`;
}

function increasePurchaseCounter() {
    const currentCounter = Number(localStorage.getItem(PURCHASE_COUNTER_KEY)) || 1;
    localStorage.setItem(PURCHASE_COUNTER_KEY, currentCounter + 1);
}

function createId(prefix) {
    if (window.crypto && window.crypto.randomUUID) {
        return window.crypto.randomUUID();
    }

    return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
}

function getPaymentStatusClass(status) {
    if (status === "Pagado") {
        return "purchase-paid";
    }

    if (status === "Abonado") {
        return "purchase-partial";
    }

    return "purchase-pending";
}

function cleanProductCode(value) {
    return value.toUpperCase().replace(/[^A-Z0-9-]/g, "").slice(0, 25);
}

function cleanCabysCode(value) {
    return value.replace(/\D/g, "").slice(0, 13);
}

function getNumber(value) {
    return Number(value) || 0;
}

function getTodayISODate() {
    return new Date().toISOString().split("T")[0];
}

function formatDate(dateValue) {
    if (!dateValue) {
        return "No especificada";
    }

    const date = new Date(dateValue + "T00:00:00");

    return date.toLocaleDateString("es-CR", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit"
    });
}

function formatPlainMoney(amount) {
    return `₡${amount.toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    })}`;
}

function escapeHTML(value) {
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}


}
