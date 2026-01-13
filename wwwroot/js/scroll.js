window.scrollToElement = function (elementId) {
    console.log('[SCROLL] 🎯 Tentando rolar para:', elementId);
    
    const element = document.getElementById(elementId);
    
    if (!element) {
        console.error('[SCROLL] ❌ Elemento NÃO encontrado:', elementId);
        console.log('[SCROLL] 📋 Elementos disponíveis:', 
            Array.from(document.querySelectorAll('[id]')).map(el => el.id));
        return false;
    }
    
    console.log('[SCROLL] ✅ Elemento encontrado:', element);
    
    try {
        const elementPosition = element.getBoundingClientRect().top + window.scrollY;
        const offsetPosition = elementPosition - 80;
        
        window.scrollTo({
            top: offsetPosition,
            behavior: 'smooth'
        });
        
        console.log('[SCROLL] ✅ Scroll executado com sucesso para Y:', offsetPosition);
        return true;
    } catch (e) {
        console.error('[SCROLL] ❌ Erro ao executar scroll:', e);
        
        try {
            element.scrollIntoView({ block: 'center' });
            console.log('[SCROLL] ⚠️ Fallback scroll executado');
            return true;
        } catch (e2) {
            console.error('[SCROLL] ❌ Fallback também falhou:', e2);
            return false;
        }
    }
};

window.scrollToSearchSection = function () {
    console.log('[SCROLL] 🔍 Scrolling to search section');
    const searchSection = document.querySelector('.search-section');
    
    if (searchSection) {
        const elementPosition = searchSection.getBoundingClientRect().top + window.scrollY;
        const offsetPosition = elementPosition - 100; // 100px do topo
        
        window.scrollTo({
            top: offsetPosition,
            behavior: 'smooth'
        });
        
        console.log('[SCROLL] ✅ Scroll para search-section executado');
        return true;
    } else {
        console.error('[SCROLL] ❌ search-section não encontrada');
        return false;
    }
};

window.scrollToTop = function () {
    console.log('[SCROLL] ⬆️ Scrolling to top');
    window.scrollTo({ 
        top: 0, 
        behavior: 'smooth' 
    });
};

// Log quando script carrega
console.log('[SCROLL] 📜 Script carregado com sucesso');