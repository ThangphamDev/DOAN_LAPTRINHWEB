// Admin area javascript functions
document.addEventListener('DOMContentLoaded', function () {
    // Initialize any admin-specific features here

    // Example: Confirm delete with custom modal
    const deleteButtons = document.querySelectorAll('.delete-confirm');
    deleteButtons.forEach(button => {
        button.addEventListener('click', function (e) {
            if (!confirm('Bạn có chắc chắn muốn xóa mục này?')) {
                e.preventDefault();
            }
        });
    });

    // Example: DataTable initialization if jQuery DataTables is included
    if (typeof $.fn.DataTable !== 'undefined') {
        $('.data-table').DataTable({
            "language": {
                "url": "//cdn.datatables.net/plug-ins/1.10.25/i18n/Vietnamese.json"
            }
        });
    }
});