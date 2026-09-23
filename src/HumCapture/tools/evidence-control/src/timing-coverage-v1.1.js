// Native evidence only; master callers filter to accepted source frames first.
export function buildCoverage(streamId, clockId, frequency, records) {
  const lanes = new Map();
  for (const r of records) {
    const lane = r.sensorStreamId ?? 0;
    if (!lanes.has(lane)) lanes.set(lane, []);
    lanes.get(lane).push(r);
  }
  const spans = [];
  for (const [lane, values] of [...lanes].sort((a, b) => a[0] - b[0])) {
    let start = 0, run = 0, gaps = 0;
    function add(end) {
      spans.push({ lane_id: lane, run_index: run++, segment_id: values[start].segmentId,
        record_count: String(end - start + 1), first_ticks: String(values[start].nativeTicks),
        last_ticks: String(values[end].nativeTicks), first_sequence: String(values[start].sequence),
        last_sequence: String(values[end].sequence), sequence_gap_count: String(gaps) });
    }
    for (let i = 1; i < values.length; i++) {
      const a = values[i - 1], b = values[i];
      if (b.segmentId !== a.segmentId || b.nativeTicks < a.nativeTicks || b.sequence <= a.sequence) {
        add(i - 1); start = i; gaps = 0;
      } else if (b.sequence - a.sequence > 1n) gaps++;
    }
    add(values.length - 1);
  }
  return { stream_id: streamId, clock_id: clockId, ticks_per_second: frequency, record_count: String(records.length), spans };
}
