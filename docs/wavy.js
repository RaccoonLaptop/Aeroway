(function () {
  var reduceQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
  var canvas = document.createElement("canvas");
  canvas.className = "app-wavy";
  canvas.setAttribute("aria-hidden", "true");
  var css = document.createElement("style");
  css.textContent = "html{background:#050b1b}body{background:transparent}.app-wavy{position:fixed;inset:0;width:100%;height:100%;z-index:-1;pointer-events:none}";
  document.head.appendChild(css);
  document.body.prepend(canvas);

  var ctx = canvas.getContext("2d");
  var particles = [];
  var width = 1;
  var height = 1;
  var running = false;
  var last = 0;
  var startedAt = 0;
  var goldGlow = null;
  var blueGlow = null;

  function reduced() {
    return reduceQuery.matches;
  }

  function makeParticle(w, h) {
    var depth = Math.random();
    var gold = Math.random() < 0.62;
    var roll = Math.random();
    var kind = roll < 0.46 ? "tri" : roll < 0.72 ? "diamond" : "dot";
    return {
      x: Math.random() * w,
      y: Math.random() * h,
      depth: depth,
      vx: (Math.random() - 0.5) * (22 + depth * 36),
      vy: (Math.random() - 0.5) * (16 + depth * 26),
      rot: Math.random() * Math.PI * 2,
      vr: (Math.random() < 0.5 ? -1 : 1) * (0.28 + Math.random() * 0.55),
      size: (kind === "dot" ? 8 : 12) + Math.random() * 18 * (0.45 + depth),
      kind: kind,
      color: gold ? "224, 178, 64" : "126, 182, 255",
      alpha: (kind === "dot" ? 0.22 : 0.16) + depth * 0.4,
      phase: Math.random() * Math.PI * 2
    };
  }

  function spawn() {
    particles.length = 0;
    var count = Math.round(Math.min(46, Math.max(22, (width * height) / 28000)));
    for (var i = 0; i < count; i++) particles.push(makeParticle(width, height));
  }

  function resize() {
    var dpr = Math.min(window.devicePixelRatio || 1, 2);
    width = Math.max(1, window.innerWidth);
    height = Math.max(1, window.innerHeight);
    canvas.width = Math.max(1, Math.floor(width * dpr));
    canvas.height = Math.max(1, Math.floor(height * dpr));
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    goldGlow = ctx.createRadialGradient(width * 0.18, 0, 0, width * 0.18, 0, width * 0.62);
    goldGlow.addColorStop(0, "rgba(224, 178, 64, 0.10)");
    goldGlow.addColorStop(1, "rgba(224, 178, 64, 0)");
    blueGlow = ctx.createRadialGradient(width * 0.86, height, 0, width * 0.86, height, Math.max(width, height) * 0.55);
    blueGlow.addColorStop(0, "rgba(18, 53, 91, 0.85)");
    blueGlow.addColorStop(1, "rgba(18, 53, 91, 0)");
    if (!particles.length) spawn();
  }

  function wrap(p) {
    var margin = 48;
    if (p.x < -margin) p.x = width + margin;
    if (p.x > width + margin) p.x = -margin;
    if (p.y < -margin) p.y = height + margin;
    if (p.y > height + margin) p.y = -margin;
  }

  function drawShape(p) {
    var s = p.size * 0.5;
    ctx.beginPath();
    if (p.kind === "dot") {
      ctx.arc(0, 0, Math.max(1.4, s * 0.22), 0, Math.PI * 2);
      ctx.fill();
      return;
    }
    if (p.kind === "diamond") {
      ctx.moveTo(0, -s * 0.72);
      ctx.lineTo(s * 0.48, 0);
      ctx.lineTo(0, s * 0.72);
      ctx.lineTo(-s * 0.48, 0);
    } else {
      ctx.moveTo(0, -s);
      ctx.lineTo(s * 0.92, s * 0.62);
      ctx.lineTo(-s * 0.92, s * 0.62);
    }
    ctx.closePath();
    ctx.stroke();
  }

  function draw(now, animate, dt) {
    ctx.clearRect(0, 0, width, height);
    ctx.fillStyle = "#050b1b";
    ctx.fillRect(0, 0, width, height);
    if (goldGlow) {
      ctx.fillStyle = goldGlow;
      ctx.fillRect(0, 0, width, height);
    }
    if (blueGlow) {
      ctx.fillStyle = blueGlow;
      ctx.fillRect(0, 0, width, height);
    }
    var t = animate ? (now - startedAt) / 1000 : 0;
    for (var i = 0; i < particles.length; i++) {
      var p = particles[i];
      if (animate) {
        var pace = 0.55 + p.depth * 0.7;
        p.x += p.vx * pace * dt;
        p.y += p.vy * pace * dt;
        p.rot += p.vr * dt;
        wrap(p);
      }
      var alpha = p.alpha;
      if (animate) alpha *= 0.72 + 0.28 * Math.sin(t * 0.85 + p.phase);
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(p.rot);
      ctx.globalAlpha = Math.max(0, Math.min(alpha, 0.7));
      ctx.lineWidth = 1.2;
      ctx.strokeStyle = "rgba(" + p.color + ", 1)";
      ctx.fillStyle = "rgba(" + p.color + ", 1)";
      drawShape(p);
      ctx.restore();
    }
  }

  function loop(now) {
    if (!running || reduced() || document.hidden) {
      running = false;
      return;
    }
    var dt = Math.min(0.05, last ? (now - last) / 1000 : 0.016);
    last = now;
    draw(now, true, dt);
    requestAnimationFrame(loop);
  }

  function startLoop() {
    if (running || reduced()) return;
    running = true;
    last = 0;
    requestAnimationFrame(loop);
  }

  resize();
  startedAt = performance.now();
  draw(startedAt, false, 0);

  window.addEventListener("resize", function () {
    resize();
    if (!running) draw(performance.now(), false, 0);
  });

  if (!reduced()) startLoop();

  document.addEventListener("visibilitychange", function () {
    if (!document.hidden) startLoop();
  });

  reduceQuery.addEventListener("change", function () {
    if (reduced()) {
      running = false;
      draw(performance.now(), false, 0);
      return;
    }
    startedAt = performance.now();
    startLoop();
  });
})();
