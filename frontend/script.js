/* JavaScript для плавного появления при скролле */
document.addEventListener('DOMContentLoaded', function() {
    // Анимация появления при скролле
    const fadeElements = document.querySelectorAll('.fade-in');
    
    const fadeInOnScroll = () => {
        fadeElements.forEach(element => {
            const elementTop = element.getBoundingClientRect().top;
            const elementVisible = 150;
            
            if (elementTop < window.innerHeight - elementVisible) {
                element.classList.add('visible');
            }
        });
    };
    
    fadeInOnScroll();
    window.addEventListener('scroll', fadeInOnScroll);
    
    // Плавный хедер при скролле
    const header = document.querySelector('.header');
    window.addEventListener('scroll', () => {
        if (window.scrollY > 50) {
            header.classList.add('scrolled');
        } else {
            header.classList.remove('scrolled');
        }
    });
    
    // Замедленная анимация для всех элементов
    document.querySelectorAll('.btn, .nav-link, .book-card, .feature-card')
        .forEach(element => {
            element.style.transition = 'all 0.4s cubic-bezier(0.25, 0.46, 0.45, 0.94)';
        });
});