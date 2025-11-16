document.addEventListener('DOMContentLoaded', function () {
  const toggleBtn = document.getElementById('toggleBtn');
  const sidebar = document.getElementById('sidebar');
  const mainContent = document.getElementById('mainContent');

  if (toggleBtn && sidebar && mainContent) {
    toggleBtn.addEventListener('click', function () {
      sidebar.classList.toggle('active');
      mainContent.classList.toggle('active');
      // No toggleBtn.classList.toggle('active'); line here
    });
  }
});