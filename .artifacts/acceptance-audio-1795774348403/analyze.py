import base64, csv, math, struct, sys, wave
from pathlib import Path

root = Path(sys.argv[1])
trace = []
for row in csv.reader((root / "rtp.tsv").open(), delimiter="\t"):
    stage, qpc, rate, channels, stamp, payload = row
    trace.append((stage, int(qpc), int(rate), int(channels), int(stamp), payload))
answer = next(qpc for name, qpc, *_ in trace if name == "answer")
with (root / "loopback.tsv").open() as data:
    source = csv.reader(data, delimiter="\t")
    freq, rate, channels, bits, encoding = next(source)
    freq, rate, channels, bits = map(int, (freq, rate, channels, bits))
    relative = lambda qpc: 1000 * (qpc - answer) / freq
    first_rtp = next((relative(qpc) for stage, qpc, *_ in trace if stage == "rtp-decoded"), None)
    print(f"Loopback {rate} Hz, {channels} channels, {bits} bits, {encoding}")
    for stage in ("answer", "ring-end", "open-output", "output-open", "rtp-arrival", "rtp-decoded", "decoder-output"):
        selected = [(relative(qpc), sr) for name, qpc, sr, *_ in trace if name == stage]
        print(stage, "ms/rate", [(round(t, 3), sr) for t, sr in selected[:4]], "count", len(selected))
    is_float = encoding.startswith("00000003-") or encoding.lower().startswith("ieee")
    pcm = [0] * (2 * rate * channels)
    measured = [False] * (2 * rate)
    counters = []
    for row in source:
        if len(row) != 6: continue
        qpc, observed, pos, flags, frames = map(int, row[:5])
        start_ms = 1000 * (qpc / 1e7 - answer / freq)
        if start_ms > 1500 or start_ms + frames * 1000 / rate < -500: continue
        if flags & 4:
            print("timestamp error", round(relative(observed), 3), frames)
            continue
        output_frame = round((start_ms + 500) * rate / 1000)
        raw = base64.b64decode(row[5])
        if is_float:
            samples = [max(-32768, min(32767, round(v * 32768))) for v in struct.unpack("<" + "f" * (frames * channels), raw)]
        elif bits == 16:
            samples = struct.unpack("<" + "h" * (frames * channels), raw)
        else:
            width = bits // 8
            samples = [int.from_bytes(raw[i:i+width], "little", signed=True) >> (bits - 16) for i in range(0, len(raw), width)]
        for frame in range(max(0, -output_frame), min(frames, len(measured) - output_frame)):
            index = output_frame + frame
            measured[index] = True
            pcm[index*channels:(index+1)*channels] = samples[frame*channels:(frame+1)*channels]
        counters.append((start_ms, start_ms + frames*1000/rate, flags))
def save(path, sr, ch, samples):
    with wave.open(str(path), "wb") as out:
        out.setparams((ch, 2, sr, 0, "NONE", "not compressed"))
        out.writeframes(struct.pack("<" + "h" * len(samples), *samples))
save(root / "WASAPI-Annahme.wav", rate, channels, pcm)
def metric(start, end):
    lo, hi = [max(0, round((x+500) * rate/1000)) for x in (start,end)]
    pairs = [(abs(pcm[i*channels+c]-pcm[(i-1)*channels+c]), (i / rate * 1000)-500, pcm[i*channels+c])
             for i in range(max(1,lo), min(hi,len(measured))) if measured[i] and measured[i-1] for c in range(channels)]
    vals = [pcm[i*channels+c] for i in range(lo, min(hi,len(measured))) if measured[i] for c in range(channels)]
    if vals:
        print(f"{start:.1f}..{end:.1f} ms: samples={len(vals)} peak={max(map(abs,vals))} rms={math.sqrt(sum(x*x for x in vals)/len(vals)):.1f} max-step={max(pairs,default=(0,0,0))}")
    else:
        print(f"{start:.1f}..{end:.1f} ms: no WASAPI samples")
for section in [(-80,0),(0,60),(60,120),(120,200),(200,300),(300,450),(450,750)]:
    metric(*section)
for stage in ["ring-end", "open-output", "output-open", "rtp-arrival", "rtp-decoded"]:
    matches = [relative(qpc) for name,qpc,*_ in trace if name==stage]
    if matches:
        t=matches[0]
        print(stage, "±10 ms")
        metric(t-10,t+10)
peaks = []
for i in range(round(0.5*rate), min(round(1.25*rate),len(measured))):
    if measured[i] and measured[i-1]:
        peaks.append((max(abs(pcm[i*channels+c]-pcm[(i-1)*channels+c]) for c in range(channels)), i*1000/rate-500))
peaks.sort(reverse=True)
events=[]
for magnitude, t in peaks:
    if all(abs(t-u)>12 for _,u in events):
        events.append((magnitude,t))
        if len(events)==12: break
print("WASAPI strongest separated steps", [(n,round(t,3)) for n,t in events])
print("Loop packets at transitions", [(round(a,2),round(b,2),f) for a,b,f in counters if -40 < a < 600 and f])
decoded=[(relative(qpc), sr,ch,stamp,base64.b64decode(payload)) for stage,qpc,sr,ch,stamp,payload in trace if stage == "rtp-decoded"]
if decoded:
    sr, ch = decoded[0][1:3]
    whole = []
    for i,(t,r,c,stamp,raw) in enumerate(decoded):
        if (r,c)!=(sr,ch): break
        values = struct.unpack("<"+"h"*(len(raw)//2),raw)
        jumps = [abs(values[j]-values[j-c]) for j in range(c,len(values))]
        if whole: jumps += [abs(values[j]-whole[-c+j]) for j in range(c)]
        whole.extend(values)
        if i < 20:
            print(f"RTP {i+1:02} {t:.2f} ms frames={len(values)//c} tsDelta={(stamp-decoded[0][3]) % 2**32} peak={max(map(abs,values),default=0)} step={max(jumps,default=0)}")
    save(root / "RTP-Erste-Frames.wav", sr, ch, whole)
