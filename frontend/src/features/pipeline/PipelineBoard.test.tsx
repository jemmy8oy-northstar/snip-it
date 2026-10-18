import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PipelineBoard as Board } from './PipelineBoard';

const PipelineBoard = () => (
  <MemoryRouter>
    <Board />
  </MemoryRouter>
);
import { STORAGE_KEY } from './storage';

beforeEach(() => window.localStorage.clear());

describe('PipelineBoard', () => {
  it('shows all five empty columns on first load', () => {
    render(<PipelineBoard />);
    for (const name of ['Idea', 'Scripted', 'Recorded', 'Cut', 'Published']) {
      expect(screen.getByRole('region', { name })).toBeInTheDocument();
    }
    expect(screen.getByText('No ideas yet. Add one above.')).toBeInTheDocument();
  });

  it('adds an idea card that lands in Idea and is saved', async () => {
    const user = userEvent.setup();
    render(<PipelineBoard />);
    expect(screen.getByRole('button', { name: 'Add idea' })).toBeDisabled();

    await user.type(screen.getByLabelText('Working title'), 'Cold open ideas');
    await user.click(screen.getByRole('button', { name: 'Add idea' }));

    const idea = screen.getByRole('region', { name: 'Idea' });
    expect(within(idea).getByText('Cold open ideas')).toBeInTheDocument();
    expect(screen.getByLabelText('Working title')).toHaveValue('');
    expect(window.localStorage.getItem(STORAGE_KEY)).toContain('Cold open ideas');
  });

  it('saving a script moves the card from Idea to Scripted', async () => {
    const user = userEvent.setup();
    render(<PipelineBoard />);
    await user.type(screen.getByLabelText('Working title'), 'Video A');
    await user.click(screen.getByRole('button', { name: 'Add idea' }));

    await user.click(screen.getByRole('button', { name: 'Open' }));
    await user.type(screen.getByLabelText('Script'), 'Hello and welcome');
    await user.click(screen.getByRole('button', { name: 'Save script' }));

    const scripted = screen.getByRole('region', { name: 'Scripted' });
    expect(within(scripted).getByText('Video A')).toBeInTheDocument();
  });

  it('saving a published link moves the card to Published and shows the link', async () => {
    const user = userEvent.setup();
    render(<PipelineBoard />);
    await user.type(screen.getByLabelText('Working title'), 'Video B');
    await user.click(screen.getByRole('button', { name: 'Add idea' }));

    await user.click(screen.getByRole('button', { name: 'Open' }));
    await user.type(screen.getByLabelText('Published link'), 'javascript:alert(1)');
    expect(screen.getByRole('button', { name: 'Save link' })).toBeDisabled();

    await user.clear(screen.getByLabelText('Published link'));
    await user.type(screen.getByLabelText('Published link'), 'https://youtu.be/abc');
    await user.click(screen.getByRole('button', { name: 'Save link' }));

    const published = screen.getByRole('region', { name: 'Published' });
    expect(within(published).getByRole('link', { name: 'Watch Video B' })).toHaveAttribute(
      'href',
      'https://youtu.be/abc',
    );
  });

  it('a new card links to the upload, tagged with its id', async () => {
    const user = userEvent.setup();
    render(<PipelineBoard />);
    await user.type(screen.getByLabelText('Working title'), 'Video C');
    await user.click(screen.getByRole('button', { name: 'Add idea' }));
    const id = JSON.parse(window.localStorage.getItem(STORAGE_KEY)!).cards[0].id;
    expect(screen.getByRole('link', { name: 'Cut Video C in the editor' })).toHaveAttribute(
      'href',
      `/editor?card=${id}`,
    );
  });

  it('starts empty over corrupt stored data', () => {
    window.localStorage.setItem(STORAGE_KEY, '{oops');
    render(<PipelineBoard />);
    expect(screen.getByText('No ideas yet. Add one above.')).toBeInTheDocument();
  });
});
