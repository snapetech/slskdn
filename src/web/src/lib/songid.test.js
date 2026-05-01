import { getForensicMatrix } from './songid';
import api from './api';

vi.mock('./api', () => ({
  default: {
    get: vi.fn(),
  },
}));

describe('songId api helpers', () => {
  beforeEach(() => {
    api.get.mockReset();
  });

  it('loads the explicit forensic matrix export endpoint', async () => {
    api.get.mockResolvedValue({
      data: {
        identityScore: 91,
        syntheticScore: 12,
      },
    });

    const matrix = await getForensicMatrix('run/id');

    expect(api.get).toHaveBeenCalledWith('/songid/runs/run%2Fid/forensic-matrix');
    expect(matrix).toEqual({
      identityScore: 91,
      syntheticScore: 12,
    });
  });
});
