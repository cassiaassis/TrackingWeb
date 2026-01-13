// Dados de exemplo
const sampleData = {
    "trackingCode": "ABCDE00001",
    "carrierCode": "DW863203770BR",
    "carrierName": "Correios",
    "idItemParceiro": "252512-00001",
    "partnerItemId": "839FEB57013D48C7944A854E03714FC5",
    "events": [
        {
            "id": "1",
            "date": "2017-06-19 15:00",
            "timestamp": "2017-06-19T15:00:00",
            "name": "Pedido entregue",
            "description": "Pedido entregue ao destinatário."
        },
        {
            "id": "2",
            "date": "2017-06-16 18:00",
            "timestamp": "2017-06-16T18:00:00",
            "name": "Nova entrega agendada",
            "description": "Nova tentativa agendada prevista para 19/jun."
        },
        {
            "id": "3",
            "date": "2017-06-16 10:40",
            "timestamp": "2017-06-16T10:40:00",
            "name": "Destinatário ausente",
            "description": "A encomenda poderá ser retirada na unidade da transportadora. Aguarde as próximas atualizações."
        },
        {
            "id": "4",
            "date": "2017-06-16 10:30",
            "timestamp": "2017-06-16T10:30:00",
            "name": "Contratempo - Possível atraso",
            "description": "A data de entrega era prevista para Sex, 16/jun e foi reagendada para Seg, 19/jun"
        }
    ]
};

// Elementos DOM
const trackingForm = document.getElementById('trackingForm');
const resultsSection = document.getElementById('resultsSection');
const emptyState = document.getElementById('emptyState');
const timeline = document.getElementById('timeline');
const loadingOverlay = document.getElementById('loadingOverlay');
const helpModal = document.getElementById('helpModal');
const closeModal = document.getElementById('closeModal');

// Mapeamento de status
const statusConfig = {
    'Pedido entregue': {
        status: 'Entregue',
        class: 'delivered',
        icon: 'fa-check-circle'
    },
    'Nova entrega agendada': {
        status: 'Agendado',
        class: 'pending',
        icon: 'fa-calendar-check'
    },
    'Destinatário ausente': {
        status: 'Pendente',
        class: 'pending',
        icon: 'fa-user-times'
    },
    'Contratempo - Possível atraso': {
        status: 'Atrasado',
        class: 'pending',
        icon: 'fa-clock'
    }
};

// Funções auxiliares
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('pt-BR', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
    });
}

function formatDateTime(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('pt-BR', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function formatDateForTimeline(dateString) {
    const date = new Date(dateString);
    const day = date.getDate();
    const month = date.toLocaleDateString('pt-BR', { month: 'short' });
    const time = date.toLocaleTimeString('pt-BR', {
        hour: '2-digit',
        minute: '2-digit'
    });
    return `${day} ${month} • ${time}`;
}

// Atualizar timeline
function updateTimeline(events) {
    timeline.innerHTML = '';

    events.forEach((event, index) => {
        const eventClass = index === 0 ? 'active' : 'completed';
        const eventIcon = index === 0 ? 'fa-check-circle' : 'fa-circle';

        const timelineItem = document.createElement('div');
        timelineItem.className = `timeline-item ${eventClass}`;

        timelineItem.innerHTML = `
            <div class="timeline-marker">
                <i class="fas ${eventIcon}"></i>
            </div>
            <div class="timeline-content">
                <div class="timeline-date">
                    <i class="far fa-calendar"></i>
                    ${formatDateForTimeline(event.date)}
                </div>
                <h3 class="timeline-title">${event.name}</h3>
                <p class="timeline-description">${event.description}</p>
            </div>
        `;

        timeline.appendChild(timelineItem);
    });
}

// Mostrar resultados
function showResults(data) {
    // Atualizar informações
    document.getElementById('displayTrackingCode').textContent = data.trackingCode;
    document.getElementById('displayCarrier').textContent = data.carrierName;
    document.getElementById('displayCarrierCode').textContent = data.carrierCode;
    document.getElementById('displayOrderNumber').textContent = data.idItemParceiro;
    document.getElementById('displayPartnerId').textContent = data.partnerItemId.substring(0, 20) + '...';

    // Calcular e mostrar data de entrega
    const lastEventDate = new Date(data.events[0].date);
    document.getElementById('displayDeliveryDate').textContent = formatDate(lastEventDate);

    // Atualizar status badge
    const currentEvent = data.events[0];
    const statusInfo = statusConfig[currentEvent.name] || { status: 'Processando', class: 'pending' };
    const statusBadge = document.getElementById('currentStatusBadge');
    statusBadge.className = `status-badge ${statusInfo.class}`;
    statusBadge.innerHTML = `<i class="fas ${statusInfo.icon}"></i> ${statusInfo.status}`;

    // Atualizar timeline
    updateTimeline(data.events);

    // Mostrar resultados e esconder estado vazio
    resultsSection.style.display = 'block';
    emptyState.style.display = 'none';

    // Scroll suave para resultados
    resultsSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

// Simular busca
function fetchTrackingData(code) {
    return new Promise((resolve) => {
        setTimeout(() => {
            if (code === 'ABCDE00001' || code === 'DW863203770BR' || code === 'TESTE001') {
                resolve(sampleData);
            } else {
                resolve(null);
            }
        }, 1500);
    });
}

// Mostrar loading
function showLoading() {
    loadingOverlay.style.display = 'flex';
}

function hideLoading() {
    loadingOverlay.style.display = 'none';
}

// Mostrar erro
function showError() {
    alert('Código não encontrado. Verifique se digitou corretamente.');
    document.getElementById('trackingCode').focus();
    document.getElementById('trackingCode').select();
}

// Event Listeners
document.addEventListener('DOMContentLoaded', function () {
    // Configurar input
    const trackingInput = document.getElementById('trackingCode');
    trackingInput.focus();
    trackingInput.select();

    // Navegação do menu
    document.querySelectorAll('.nav-link').forEach(link => {
        link.addEventListener('click', function (e) {
            e.preventDefault();
            // Remove active class de todos os itens
            document.querySelectorAll('.nav-item').forEach(item => {
                item.classList.remove('active');
            });
            // Adiciona active class no item clicado
            this.closest('.nav-item').classList.add('active');
        });
    });

    // Form submission
    trackingForm.addEventListener('submit', async function (e) {
        e.preventDefault();

        const code = trackingInput.value.trim();
        if (!code) {
            trackingInput.focus();
            return;
        }

        showLoading();

        try {
            const data = await fetchTrackingData(code);
            hideLoading();

            if (data) {
                showResults(data);
            } else {
                showError();
            }
        } catch (error) {
            hideLoading();
            showError();
            console.error('Erro:', error);
        }
    });

    // Modal
    const helpButton = document.getElementById('helpButton');
    if (helpButton) {
        helpButton.addEventListener('click', function () {
            helpModal.style.display = 'flex';
        });
    }

    if (closeModal) {
        closeModal.addEventListener('click', function () {
            helpModal.style.display = 'none';
        });
    }

    if (helpModal) {
        helpModal.addEventListener('click', function (e) {
            if (e.target === helpModal) {
                helpModal.style.display = 'none';
            }
        });
    }
});

// Para teste rápido: descomente a linha abaixo
// window.addEventListener('load', () => trackingForm.dispatchEvent(new Event('submit')));