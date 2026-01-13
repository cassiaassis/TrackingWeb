// Dados de exemplo para quando encontrar
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

// Dados para quando não encontrar
const notFoundData = {
    "message": "CPF ou e-mail não localizado."
};

// Elementos DOM
const trackingForm = document.getElementById('trackingForm');
const resultsSection = document.getElementById('resultsSection');
const emptyState = document.getElementById('emptyState');
const timeline = document.getElementById('timeline');
const loadingOverlay = document.getElementById('loadingOverlay');
const helpModal = document.getElementById('helpModal');
const closeModal = document.getElementById('closeModal');

// Novos elementos para estados
const notFoundState = document.getElementById('notFoundState');
const foundState = document.getElementById('foundState');
const errorMessage = document.getElementById('errorMessage');
const tryAgainButton = document.getElementById('tryAgainButton');
const contactSupportButton = document.getElementById('contactSupportButton');

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
            <div class="timeline-content-inner">
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

// Mostrar estado: ENCONTRADO
function showFoundState(data) {
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
    
    // Mostrar estado encontrado e esconder outros
    resultsSection.style.display = 'block';
    foundState.style.display = 'block';
    notFoundState.style.display = 'none';
    emptyState.style.display = 'none';
    
    // Scroll suave para resultados
    resultsSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

// Mostrar estado: NÃO ENCONTRADO
function showNotFoundState(errorData) {
    // Atualizar mensagem de erro
    errorMessage.textContent = errorData.message || "Código não encontrado. Verifique se digitou corretamente.";
    
    // Mostrar estado não encontrado e esconder outros
    resultsSection.style.display = 'block';
    notFoundState.style.display = 'block';
    foundState.style.display = 'none';
    emptyState.style.display = 'none';
    
    // Scroll suave para resultados
    resultsSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

// Simular busca com possibilidade de não encontrar
function fetchTrackingData(code) {
    return new Promise((resolve, reject) => {
        setTimeout(() => {
            // Códigos válidos
            const validCodes = ['ABCDE00001', 'DW863203770BR', 'TESTE001'];
            
            if (validCodes.includes(code)) {
                resolve({
                    success: true,
                    data: sampleData
                });
            } else {
                resolve({
                    success: false,
                    data: notFoundData
                });
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

// Função para resetar o formulário e voltar ao estado inicial
function resetToInitialState() {
    const trackingInput = document.getElementById('trackingCode');
    trackingInput.value = '';
    trackingInput.focus();
    
    resultsSection.style.display = 'none';
    notFoundState.style.display = 'none';
    foundState.style.display = 'none';
    emptyState.style.display = 'block';
}

// Event Listeners
document.addEventListener('DOMContentLoaded', function() {
    // Configurar input
    const trackingInput = document.getElementById('trackingCode');
    trackingInput.focus();
    trackingInput.select();
    
    // Form submission
    trackingForm.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const code = trackingInput.value.trim();
        if (!code) {
            trackingInput.focus();
            return;
        }
        
        showLoading();
        
        try {
            const result = await fetchTrackingData(code);
            hideLoading();
            
            if (result.success) {
                showFoundState(result.data);
            } else {
                showNotFoundState(result.data);
            }
        } catch (error) {
            hideLoading();
            showNotFoundState({ message: "Erro na busca. Tente novamente." });
            console.error('Erro:', error);
        }
    });
    
    // Botão "Tentar Novamente"
    if (tryAgainButton) {
        tryAgainButton.addEventListener('click', function() {
            resetToInitialState();
        });
    }
    
    // Botão "Falar com Suporte"
    if (contactSupportButton) {
        contactSupportButton.addEventListener('click', function() {
            helpModal.style.display = 'flex';
        });
    }
    
    // Modal
    const helpButton = document.getElementById('helpButton');
    if (helpButton) {
        helpButton.addEventListener('click', function() {
            helpModal.style.display = 'flex';
        });
    }
    
    if (closeModal) {
        closeModal.addEventListener('click', function() {
            helpModal.style.display = 'none';
        });
    }
    
    if (helpModal) {
        helpModal.addEventListener('click', function(e) {
            if (e.target === helpModal) {
                helpModal.style.display = 'none';
            }
        });
    }
});

// Para teste rápido: descomente a linha abaixo
// window.addEventListener('load', () => trackingForm.dispatchEvent(new Event('submit')));