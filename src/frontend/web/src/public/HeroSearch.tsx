import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { MapContainer, TileLayer, Marker, Popup } from "react-leaflet";
import MarkerClusterGroup from "react-leaflet-cluster";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import markerIcon2x from "leaflet/dist/images/marker-icon-2x.png";
import markerIcon from "leaflet/dist/images/marker-icon.png";
import markerShadow from "leaflet/dist/images/marker-shadow.png";
import { getPublicPrograms, type PublicProgram } from "./publicApi";
import { cityCoordinates, US_CENTER, US_ZOOM } from "./usCityCoordinates";

// Point Leaflet's default marker at the bundled images (its CSS-relative URLs break under bundlers).
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow
});

const PRICE_BANDS = [
  { label: "$1001 - $2000", min: 1001, max: 2000 },
  { label: "$2001 - $3000", min: 2001, max: 3000 },
  { label: "$3001 - $4000", min: 3001, max: 4000 },
  { label: "> $5001", min: 5001, max: Number.POSITIVE_INFINITY }
];

// "Clinical Needs" program tags, verbatim from the live hero (NewHome.js): "Most Popular" first,
// the rest alphabetical. The anonymous public feed carries no tag data, so picking one routes the
// visitor into sign-in (the real, tagged catalog lives behind login) — matching the live site, where
// engaging the search surfaces the login wall.
const CLINICAL_NEEDS = [
  "Most Popular", "100% Inpatient Hospitalist", "Academic Affiliation", "Core", "Discount Available",
  "Elective", "Faculty", "Hands On", "Hospital Invitation Letter", "Hospital Letterhead LOR",
  "Housing available", "IMG Friendly", "Inpatient", "Instant Approval", "Publication", "Research",
  "Residency Audition", "Some Inpatient", "UPike"
];

// Ratings options as the live hero renders them (highest first). Login-gated like Clinical Needs.
const RATING_OPTIONS = ["5", "4", "3", "2", "1"];

// Sort options, verbatim from the live hero results header. Sorting runs over the full catalog, so it
// is login-gated for anonymous visitors.
const SORT_OPTIONS = [
  { value: "nameAsc", label: "Program Name (A-Z)" },
  { value: "nameDesc", label: "Program Name (Z-A)" },
  { value: "priceAsc", label: "Price (Low to High)" },
  { value: "priceDesc", label: "Price (High to Low)" },
  { value: "reviewsAsc", label: "Reviews (Low to High)" },
  { value: "reviewsDesc", label: "Reviews (High to Low)" }
];

function typeLabel(t: string): string {
  if (t.toLowerCase().startsWith("inperson") || t.toLowerCase() === "inperson") return "InPerson";
  if (t.toLowerCase().startsWith("tele")) return "TeleRotation";
  return t;
}

function totalPrice(p: PublicProgram): number {
  return p.retailAmountPerWeek * p.minWeeksPerRotation;
}

/** The landing hero: a program search bar + filter dropdowns, an interactive (Leaflet) US map with
 *  clustered markers, and a results preview — cloning the live www.rotationsplus.org hero. The data is
 *  the anonymous public feed; the filters narrow the preview live. Because the landing is anonymous,
 *  the **Search** button and each **View program** prompt sign-in (route to the customer portal /
 *  CIAM), matching the live site's behaviour of gating the real search behind login. */
export function HeroSearch() {
  const navigate = useNavigate();
  const [programs, setPrograms] = useState<PublicProgram[]>([]);
  const [term, setTerm] = useState("");
  const [specialty, setSpecialty] = useState("");
  const [programType, setProgramType] = useState("");
  const [city, setCity] = useState("");
  const [state, setState] = useState("");
  const [duration, setDuration] = useState("");
  const [price, setPrice] = useState("");

  useEffect(() => {
    const ac = new AbortController();
    void getPublicPrograms(ac.signal).then(setPrograms);
    return () => ac.abort();
  }, []);

  // Distinct dropdown options derived from the feed.
  const specialties = useMemo(
    () => [...new Set(programs.map((p) => p.specialtyName))].sort(),
    [programs]
  );
  const cities = useMemo(
    () => [...new Set(programs.map((p) => p.city).filter((c): c is string => !!c))].sort(),
    [programs]
  );
  const states = useMemo(
    () => [...new Set(programs.map((p) => p.state).filter((s): s is string => !!s))].sort(),
    [programs]
  );
  const durations = useMemo(
    () => [...new Set(programs.map((p) => p.minWeeksPerRotation))].sort((a, b) => a - b),
    [programs]
  );

  const filtered = useMemo(() => {
    const t = term.trim().toLowerCase();
    return programs.filter((p) => {
      if (t && !`${p.specialtyName} ${p.city ?? ""} ${p.state ?? ""}`.toLowerCase().includes(t)) return false;
      if (specialty && p.specialtyName !== specialty) return false;
      if (programType && typeLabel(p.programType) !== programType) return false;
      if (city && p.city !== city) return false;
      if (state && p.state !== state) return false;
      if (duration && p.minWeeksPerRotation !== Number(duration)) return false;
      if (price) {
        const band = PRICE_BANDS.find((b) => b.label === price);
        if (band) {
          const tot = totalPrice(p);
          if (tot < band.min || tot > band.max) return false;
        }
      }
      return true;
    });
  }, [programs, term, specialty, programType, city, state, duration, price]);

  // Cluster markers by mapped city coordinates.
  const markers = useMemo(
    () =>
      filtered
        .map((p) => ({ p, coords: cityCoordinates(p.city) }))
        .filter((m): m is { p: PublicProgram; coords: [number, number] } => m.coords !== null),
    [filtered]
  );

  /** Anonymous visitors can't search/open a program — send them into the customer sign-in/up. */
  const requireLogin = () => navigate("/portal");

  const hasActiveFilters = !!(term || specialty || programType || city || state || duration || price);
  const resetFilters = () => {
    setTerm("");
    setSpecialty("");
    setProgramType("");
    setCity("");
    setState("");
    setDuration("");
    setPrice("");
  };

  return (
    <section className="hero hero-search">
      <h1 className="hero-title">
        Find Your Perfect <span className="hero-accent">Clinical Experience</span> Today
      </h1>
      <p className="hero-sub">
        Gain Valuable Clinical Experience and Earn Letters of Recommendations to Make Your Medical
        Residency, D.O. or Dental Goals a Reality!
      </p>

      {/* Search bar */}
      <form
        className="search-bar"
        onSubmit={(e) => {
          e.preventDefault();
          requireLogin();
        }}
      >
        <span className="search-ico" aria-hidden="true">🔍</span>
        <input
          className="search-input"
          placeholder="What are you searching for?"
          value={term}
          onChange={(e) => setTerm(e.target.value)}
          aria-label="Search programs"
        />
        <button type="submit" className="btn btn-primary search-btn">Search</button>
      </form>

      {/* Filter dropdowns */}
      <div className="search-filters">
        <select aria-label="Specialties" value={specialty} onChange={(e) => setSpecialty(e.target.value)}>
          <option value="">Specialties</option>
          {specialties.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select aria-label="Program Type" value={programType} onChange={(e) => setProgramType(e.target.value)}>
          <option value="">Program Type</option>
          <option value="InPerson">InPerson</option>
          <option value="TeleRotation">TeleRotation</option>
        </select>
        <select aria-label="Clinical Needs" value="" onChange={requireLogin}>
          <option value="">Clinical Needs</option>
          {CLINICAL_NEEDS.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
        </select>
        <select aria-label="Ratings" value="" onChange={requireLogin}>
          <option value="">Ratings</option>
          {RATING_OPTIONS.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
        <select aria-label="City" value={city} onChange={(e) => setCity(e.target.value)}>
          <option value="">City</option>
          {cities.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        <select aria-label="State" value={state} onChange={(e) => setState(e.target.value)}>
          <option value="">State</option>
          {states.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select aria-label="Duration" value={duration} onChange={(e) => setDuration(e.target.value)}>
          <option value="">Duration</option>
          {durations.map((d) => <option key={d} value={d}>{d} weeks</option>)}
        </select>
        <select aria-label="Pricing" value={price} onChange={(e) => setPrice(e.target.value)}>
          <option value="">Pricing</option>
          {PRICE_BANDS.map((b) => <option key={b.label} value={b.label}>{b.label}</option>)}
        </select>
      </div>

      {/* Map + results */}
      <div className="search-results-grid">
        <div className="search-map">
          <MapContainer center={US_CENTER} zoom={US_ZOOM} scrollWheelZoom={false} style={{ height: "100%", width: "100%" }}>
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <MarkerClusterGroup chunkedLoading>
              {markers.map((m) => (
                <Marker key={m.p.id} position={m.coords}>
                  <Popup>
                    <strong>{m.p.specialtyName}</strong>
                    <br />
                    {m.p.city}, {m.p.state}
                    <br />${totalPrice(m.p).toLocaleString()}
                  </Popup>
                </Marker>
              ))}
            </MarkerClusterGroup>
          </MapContainer>
        </div>

        <div className="search-results-col">
          <div className="search-results-head">
            {hasActiveFilters ? (
              <button type="button" className="search-reset" onClick={resetFilters}>Reset filters</button>
            ) : (
              <span />
            )}
            <select aria-label="Sort by" className="search-sort" value="" onChange={requireLogin}>
              <option value="">Sort by...</option>
              {SORT_OPTIONS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
          </div>

          <ul className="search-results" aria-label="Program results">
            {filtered.length === 0 && <li className="search-empty">No programs match your filters.</li>}
          {filtered.map((p) => (
            <li className="result-card" key={p.id}>
              <div className="result-body">
                <div className="result-spec">{p.specialtyName}</div>
                <div className="result-loc">📍 {p.city}, {p.state}</div>
                <div className="result-price">${totalPrice(p).toLocaleString()}</div>
                <div className="result-weeks">{p.minWeeksPerRotation} weeks minimum</div>
              </div>
              <div className="result-side">
                {p.instantApproval && <span className="result-instant">Instant Approval</span>}
                <button className="btn btn-link result-view" onClick={requireLogin}>View program →</button>
              </div>
            </li>
          ))}
          </ul>
        </div>
      </div>
    </section>
  );
}
