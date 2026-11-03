import os
import sqlite3
import json
from pathlib import Path
from datetime import datetime
from mcp.server.fastmcp import FastMCP

mcp = FastMCP("EcoGlamp-MCP-Server")

def get_db_connection():
    candidates = [
        Path(__file__).parent / "src" / "WebLab" / "glamping.db",
        Path(__file__).parent / "glamping.db",
        Path.cwd() / "src" / "WebLab" / "glamping.db",
        Path.cwd() / "glamping.db"
    ]
    for p in candidates:
        if p.exists():
            conn = sqlite3.connect(str(p.resolve()))
            conn.row_factory = sqlite3.Row
            return conn
    raise FileNotFoundError("glamping.db database file not found")

@mcp.tool()
def search_glamping_sites(query: str = "", region: str = "", max_price: float = 0.0) -> str:
    """Search glamping sites by text query, region name, and maximum price per night in UAH."""
    conn = get_db_connection()
    try:
        sql = """
            SELECT g.Id, g.Name, g.Description, g.Address, g.PricePerNight, g.MaxGuests, 
                   r.Name AS RegionName
            FROM GlampingSites g
            LEFT JOIN Regions r ON g.RegionId = r.Id
            WHERE 1=1
        """
        params = []
        if query:
            sql += " AND (g.Name LIKE ? OR g.Description LIKE ?)"
            params.extend([f"%{query}%", f"%{query}%"])
        if region:
            sql += " AND r.Name LIKE ?"
            params.append(f"%{region}%")
        if max_price > 0:
            sql += " AND g.PricePerNight <= ?"
            params.append(max_price)
        sql += " ORDER BY g.PricePerNight ASC"

        rows = conn.execute(sql, params).fetchall()
        results = [dict(row) for row in rows]
        if not results and (region or max_price > 0):
            fallback_sql = """
                SELECT g.Id, g.Name, g.Description, g.Address, g.PricePerNight, g.MaxGuests, 
                       r.Name AS RegionName
                FROM GlampingSites g
                LEFT JOIN Regions r ON g.RegionId = r.Id
            """
            fallback_params = []
            if region:
                fallback_sql += " WHERE r.Name LIKE ?"
                fallback_params.append(f"%{region}%")
            fallback_sql += " ORDER BY g.PricePerNight ASC"
            fallback_rows = conn.execute(fallback_sql, fallback_params).fetchall()
            results = [dict(row) for row in fallback_rows]
            if not results:
                all_rows = conn.execute("""
                    SELECT g.Id, g.Name, g.Description, g.Address, g.PricePerNight, g.MaxGuests, 
                           r.Name AS RegionName
                    FROM GlampingSites g
                    LEFT JOIN Regions r ON g.RegionId = r.Id
                    ORDER BY g.PricePerNight ASC
                """).fetchall()
                results = [dict(row) for row in all_rows]
        return json.dumps({"count": len(results), "glampings": results}, ensure_ascii=False, indent=2)
    finally:
        conn.close()

@mcp.tool()
def get_glamping_details(site_id: int) -> str:
    """Retrieve full details of a specific glamping site by its ID, including amenities and reviews."""
    conn = get_db_connection()
    try:
        site_row = conn.execute("""
            SELECT g.Id, g.Name, g.Description, g.Address, g.Latitude, g.Longitude, 
                   g.PricePerNight, g.MaxGuests, g.ContactPhone, r.Name AS RegionName
            FROM GlampingSites g
            LEFT JOIN Regions r ON g.RegionId = r.Id
            WHERE g.Id = ?
        """, (site_id,)).fetchone()

        if not site_row:
            return json.dumps({"error": f"Glamping site with ID {site_id} not found"}, ensure_ascii=False)

        site = dict(site_row)

        activities_rows = conn.execute("""
            SELECT a.Name, a.IconClass
            FROM Activities a
            INNER JOIN GlampingActivities ga ON a.Id = ga.ActivityId
            WHERE ga.GlampingSiteId = ?
        """, (site_id,)).fetchall()
        site["activities"] = [dict(a) for a in activities_rows]

        reviews_rows = conn.execute("""
            SELECT Id, AuthorName, Rating, Comment, CreatedAt
            FROM Reviews
            WHERE GlampingSiteId = ?
            ORDER BY CreatedAt DESC
        """, (site_id,)).fetchall()
        site["reviews"] = [dict(r) for r in reviews_rows]
        site["reviews_count"] = len(site["reviews"])
        site["average_rating"] = round(sum(r["Rating"] for r in site["reviews"]) / len(site["reviews"]), 1) if site["reviews"] else 0.0

        return json.dumps(site, ensure_ascii=False, indent=2)
    finally:
        conn.close()

@mcp.tool()
def get_eco_statistics() -> str:
    """Get aggregate statistics across all glamping locations in Ukraine: counts, average prices, and regions."""
    conn = get_db_connection()
    try:
        total_sites = conn.execute("SELECT COUNT(*) FROM GlampingSites").fetchone()[0]
        avg_price = conn.execute("SELECT ROUND(AVG(PricePerNight), 0) FROM GlampingSites").fetchone()[0] or 0
        total_reviews = conn.execute("SELECT COUNT(*) FROM Reviews").fetchone()[0]

        regions_data = conn.execute("""
            SELECT r.Name, COUNT(g.Id) AS SitesCount, ROUND(AVG(g.PricePerNight), 0) AS AvgPrice
            FROM Regions r
            LEFT JOIN GlampingSites g ON r.Id = g.RegionId
            GROUP BY r.Id, r.Name
            ORDER BY SitesCount DESC
        """).fetchall()

        activities_data = conn.execute("""
            SELECT a.Name, COUNT(ga.GlampingSiteId) AS SitesCount
            FROM Activities a
            LEFT JOIN GlampingActivities ga ON a.Id = ga.ActivityId
            GROUP BY a.Id, a.Name
            ORDER BY SitesCount DESC
        """).fetchall()

        stats = {
            "total_glamping_sites": total_sites,
            "average_price_per_night_uah": avg_price,
            "total_reviews": total_reviews,
            "regions_distribution": [dict(r) for r in regions_data],
            "popular_activities": [dict(a) for a in activities_data]
        }
        return json.dumps(stats, ensure_ascii=False, indent=2)
    finally:
        conn.close()

@mcp.tool()
def add_review(site_id: int, author_name: str, rating: int, comment: str) -> str:
    """Add a verified guest review and rating (1 to 5) for a specific glamping site."""
    if not (1 <= rating <= 5):
        return json.dumps({"error": "Rating must be between 1 and 5"}, ensure_ascii=False)
    if not author_name or not author_name.strip():
        return json.dumps({"error": "Author name is required"}, ensure_ascii=False)

    conn = get_db_connection()
    try:
        site_exists = conn.execute("SELECT Name FROM GlampingSites WHERE Id = ?", (site_id,)).fetchone()
        if not site_exists:
            return json.dumps({"error": f"Glamping site with ID {site_id} not found"}, ensure_ascii=False)

        site_name = site_exists[0]
        now_str = datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S")

        cursor = conn.cursor()
        cursor.execute("""
            INSERT INTO Reviews (AuthorName, Rating, Comment, CreatedAt, GlampingSiteId)
            VALUES (?, ?, ?, ?, ?)
        """, (author_name.strip(), rating, comment.strip(), now_str, site_id))
        new_id = cursor.lastrowid
        conn.commit()

        return json.dumps({
            "status": "success",
            "message": f"Review successfully posted for '{site_name}'",
            "review_id": new_id,
            "author": author_name.strip(),
            "rating": rating,
            "comment": comment.strip()
        }, ensure_ascii=False, indent=2)
    finally:
        conn.close()

if __name__ == "__main__":
    mcp.run()
