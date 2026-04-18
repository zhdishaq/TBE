// Plan 05-04 Task 3 — BookingsPager.
//
// Contract:
//   - Page-size selector with 20, 50, 100 (D-44).
//   - Prev/Next buttons call onNavigate with the new page number.
//   - Prev disabled on page 1, Next disabled when page*size >= total.
//   - nuqs-friendly: pure component (reads current page/size from props,
//     emits onNavigate/onSizeChange — URL sync happens in the parent).

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BookingsPager } from '@/components/bookings/pager';

describe('BookingsPager', () => {
  it('renders the three page-size options', () => {
    render(
      <BookingsPager page={1} size={20} total={500} onNavigate={() => {}} onSizeChange={() => {}} />,
    );
    // Native <select> — the listbox is the select itself.
    const select = screen.getByRole('combobox', { name: /page size/i });
    expect(select).toBeInTheDocument();
    const options = Array.from(select.querySelectorAll('option')).map((o) => o.value);
    expect(options).toEqual(['20', '50', '100']);
  });

  it('disables the previous button on page 1', () => {
    render(
      <BookingsPager page={1} size={20} total={500} onNavigate={() => {}} onSizeChange={() => {}} />,
    );
    expect(screen.getByRole('button', { name: /previous page/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /next page/i })).not.toBeDisabled();
  });

  it('disables the next button on the last page', () => {
    // 20 rows per page, 20 total → page 1 is last.
    render(
      <BookingsPager page={1} size={20} total={20} onNavigate={() => {}} onSizeChange={() => {}} />,
    );
    expect(screen.getByRole('button', { name: /next page/i })).toBeDisabled();
  });

  it('calls onNavigate with the new page number on prev/next click', async () => {
    const onNavigate = vi.fn();
    render(
      <BookingsPager
        page={2}
        size={20}
        total={500}
        onNavigate={onNavigate}
        onSizeChange={() => {}}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: /previous page/i }));
    expect(onNavigate).toHaveBeenCalledWith(1);
    await userEvent.click(screen.getByRole('button', { name: /next page/i }));
    expect(onNavigate).toHaveBeenCalledWith(3);
  });

  it('calls onSizeChange when the select changes', async () => {
    const onSizeChange = vi.fn();
    render(
      <BookingsPager
        page={1}
        size={20}
        total={500}
        onNavigate={() => {}}
        onSizeChange={onSizeChange}
      />,
    );
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /page size/i }), '50');
    expect(onSizeChange).toHaveBeenCalledWith(50);
  });
});
